// GitHub の README.md と docs/*.md を Notion のページへ写す。
//
// 使い方:
//   NOTION_TOKEN=... node sync.mjs --all            全部
//   NOTION_TOKEN=... node sync.mjs docs/仕様書.md   指定したファイルだけ
//   node sync.mjs --all --dry-run [--dump]          Notion に書かずに変換だけ確かめる
//
// 対応表は config.json。対応表にない docs/*.md は、親ページの下に
// 同じタイトル（ファイル先頭の # 見出し）のページを探し、なければ作る。

import { readFileSync, readdirSync, existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import notionPkg from '@notionhq/client';
import martian from '@tryfabric/martian';

const { Client } = notionPkg;
const { markdownToBlocks } = martian;

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, '../..');
const config = JSON.parse(readFileSync(path.join(here, 'config.json'), 'utf8'));

const args = process.argv.slice(2);
const dryRun = args.includes('--dry-run');
const all = args.includes('--all');
const commit = (process.env.GITHUB_SHA || 'local').slice(0, 7);
const branch = config.branch;
const ghBase = `https://github.com/${config.repo}`;

// Notion のコードブロックで使える言語（これ以外は plain text にする）
const CODE_LANGS = new Set(['abap', 'arduino', 'bash', 'basic', 'c', 'clojure', 'coffeescript', 'c++', 'c#', 'css', 'dart', 'diff', 'docker', 'elixir', 'elm', 'erlang', 'flow', 'fortran', 'f#', 'gherkin', 'glsl', 'go', 'graphql', 'groovy', 'haskell', 'html', 'java', 'javascript', 'json', 'julia', 'kotlin', 'latex', 'less', 'lisp', 'livescript', 'lua', 'makefile', 'markdown', 'markup', 'matlab', 'mermaid', 'nix', 'objective-c', 'ocaml', 'pascal', 'perl', 'php', 'plain text', 'powershell', 'prolog', 'protobuf', 'python', 'r', 'reason', 'ruby', 'rust', 'sass', 'scala', 'scheme', 'scss', 'shell', 'sql', 'swift', 'typescript', 'vb.net', 'verilog', 'vhdl', 'visual basic', 'webassembly', 'xml', 'yaml', 'java/c/c++/c#']);
const LANG_ALIAS = { hlsl: 'c++', cs: 'c#', csharp: 'c#', sh: 'shell', js: 'javascript', ts: 'typescript', yml: 'yaml' };

// ---------- 対象ファイル ----------

function listDocs() {
  const files = ['README.md'];
  for (const name of readdirSync(path.join(repoRoot, 'docs')).sort()) {
    if (name.endsWith('.md')) files.push(`docs/${name}`);
  }
  return files;
}

function targetFiles() {
  if (all) return listDocs();
  const given = args.filter((a) => !a.startsWith('--'));
  return given.filter((f) => f === 'README.md' || (f.startsWith('docs/') && f.endsWith('.md')));
}

// ---------- Markdown の下ごしらえ ----------

function splitTitle(md) {
  const m = md.match(/^﻿?#\s+(.+)\r?\n/);
  if (!m) return { title: null, body: md };
  return { title: m[1].trim(), body: md.slice(m[0].length) };
}

// 相対リンクを、Notion ページ（対応表にあれば）か GitHub の URL に直す
function rewriteLinks(md, file, pageUrls) {
  const dir = path.posix.dirname(file);
  return md.replace(/(!?)\[([^\]]*)\]\(([^)\s]+)\)/g, (all, bang, text, href) => {
    if (/^[a-z]+:|^#|^\/\//i.test(href)) return all;
    const [p, hash] = href.split('#');
    const target = path.posix.normalize(path.posix.join(dir === '.' ? '' : dir, decodeURI(p)));
    if (!bang && pageUrls[target]) return `[${text}](${pageUrls[target]})`;
    const kind = bang ? 'raw' : 'blob';
    const url = `${ghBase}/${kind}/${branch}/${encodeURI(target)}${hash ? '#' + hash : ''}`;
    return `${bang}[${text}](${url})`;
  });
}

// 行末の半角スペース2つ（改行）は、ふつうの改行にしておく（Notion でも改行になる）
// 区切り線 --- は変換で消えるので、目印に置き換えてあとで divider にする
const DIVIDER = '@@NOTION_DIVIDER@@';
function prepare(md) {
  const lines = md.split(/\r?\n/);
  let fence = null;
  return lines
    .map((line, i) => {
      const f = line.match(/^\s*(`{3,}|~{3,})/);
      if (f) {
        if (!fence) {
          fence = f[1];
          const lang = line.trim().slice(fence.length).trim().toLowerCase();
          if (LANG_ALIAS[lang]) return line.replace(/(`{3,}|~{3,})\s*\S+/, `$1${LANG_ALIAS[lang]}`);
        }
        else if (line.trim().startsWith(fence)) fence = null;
        return line;
      }
      if (fence) return line;
      if (/^\s*(-{3,}|\*{3,}|_{3,})\s*$/.test(line) && (i === 0 || lines[i - 1].trim() === '')) return DIVIDER;
      return line.replace(/ {2,}$/, '');
    })
    .join('\n');
}

function fixBlocks(blocks) {
  for (const [i, b] of blocks.entries()) {
    const text = b.paragraph?.rich_text;
    if (text && text.length === 1 && text[0].text?.content === DIVIDER) {
      blocks[i] = { object: 'block', type: 'divider', divider: {} };
      continue;
    }
    if (b.type === 'code') {
      let lang = (b.code.language || '').toLowerCase();
      lang = LANG_ALIAS[lang] || lang;
      b.code.language = CODE_LANGS.has(lang) ? lang : 'plain text';
    }
    const inner = b[b.type];
    if (inner && Array.isArray(inner.children)) fixBlocks(inner.children);
  }
  return blocks;
}

function headerCallout(file) {
  const url = `${ghBase}/blob/${branch}/${encodeURI(file)}`;
  const t = (content, link) => ({ type: 'text', text: { content, link: link ? { url: link } : null } });
  return {
    object: 'block',
    type: 'callout',
    callout: {
      icon: { type: 'emoji', emoji: '🔗' },
      color: 'gray_background',
      rich_text: [
        t('元ファイル：'),
        t(file, url),
        t(`（GitHub から自動で写しています。直すときは GitHub の方を直してください。最終同期のコミット ${commit}）`),
      ],
    },
  };
}

function buildBlocks(file, pageUrls) {
  const raw = readFileSync(path.join(repoRoot, file), 'utf8');
  const { title, body } = splitTitle(raw);
  const md = prepare(rewriteLinks(body, file, pageUrls));
  const blocks = fixBlocks(markdownToBlocks(md, { notionLimits: { truncate: true } }));
  return { title, blocks: [headerCallout(file), ...blocks] };
}

// ---------- Notion ----------

let notion = null;

function requireToken() {
  const t = process.env.NOTION_TOKEN;
  if (!t) {
    console.error('NOTION_TOKEN がありません。');
    process.exit(1);
  }
  return t;
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function call(fn) {
  for (let i = 0; ; i++) {
    try {
      return await fn();
    } catch (e) {
      const retry = e.status === 429 || (e.status >= 500 && e.status < 600) || e.code === 'notionhq_client_request_timeout';
      if (!retry || i >= 5) throw e;
      await sleep(1000 * 2 ** i);
    }
  }
}

async function listChildren(blockId) {
  const out = [];
  let cursor;
  do {
    const res = await call(() => notion.blocks.children.list({ block_id: blockId, start_cursor: cursor, page_size: 100 }));
    out.push(...res.results);
    cursor = res.has_more ? res.next_cursor : undefined;
  } while (cursor);
  return out;
}

async function findOrCreatePage(title) {
  const kids = await listChildren(config.parentPageId);
  const hit = kids.find((b) => b.type === 'child_page' && b.child_page.title === title);
  if (hit) return hit.id;
  const page = await call(() =>
    notion.pages.create({
      parent: { page_id: config.parentPageId },
      properties: { title: { title: [{ type: 'text', text: { content: title } }] } },
    }),
  );
  console.log(`  新しいページを作りました: ${title}（config.json に足すと確実です: ${page.id}）`);
  return page.id;
}

async function replaceContent(pageId, blocks) {
  // 子ページ・子データベースは消さない
  const old = (await listChildren(pageId)).filter((b) => b.type !== 'child_page' && b.type !== 'child_database');
  for (const b of old) {
    await call(() => notion.blocks.delete({ block_id: b.id }));
    await sleep(340);
  }
  for (let i = 0; i < blocks.length; i += 100) {
    await call(() => notion.blocks.children.append({ block_id: pageId, children: blocks.slice(i, i + 100) }));
    await sleep(340);
  }
}

// ---------- 本体 ----------

const toUrl = (id) => `https://www.notion.so/${id.replace(/-/g, '')}`;

async function main() {
  const files = targetFiles();
  if (files.length === 0) {
    console.log('同期するファイルはありません。');
    return;
  }

  if (!dryRun) notion = new Client({ auth: requireToken() });

  const pageUrls = {};
  for (const [f, id] of Object.entries(config.pages)) pageUrls[f] = toUrl(id);

  let failed = 0;
  for (const file of files) {
    if (!existsSync(path.join(repoRoot, file))) {
      console.log(`- ${file}: ファイルが無いので飛ばします（Notion のページは残ります）`);
      continue;
    }
    try {
      const { title, blocks } = buildBlocks(file, pageUrls);
      if (dryRun) {
        const types = {};
        for (const b of blocks) types[b.type] = (types[b.type] || 0) + 1;
        console.log(`- ${file}: ${blocks.length} ブロック`, types);
        if (args.includes('--dump')) console.log(JSON.stringify(blocks, null, 1));
        continue;
      }
      let pageId = config.pages[file];
      if (!pageId) pageId = await findOrCreatePage(title || path.basename(file, '.md'));
      await replaceContent(pageId, blocks);
      console.log(`- ${file}: 同期しました（${blocks.length} ブロック）`);
    } catch (e) {
      failed++;
      console.error(`- ${file}: 失敗しました: ${e.message}`);
    }
  }
  if (failed) process.exit(1);
}

main();
