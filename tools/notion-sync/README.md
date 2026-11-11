# notion-sync

`README.md` と `docs/*.md` を、Notion の「錦鯉プロジェクト / GitHubドキュメント（Nishiki）」の下のページへ写す仕組み。

- main に push して README か docs が変わると、GitHub Actions（`.github/workflows/notion-sync.yml`）が変わったファイルだけ写す
- Actions の画面の「Notion へドキュメントを同期」→「Run workflow」で、全部を写し直せる
- Notion 側は読む用の写し。直すときは GitHub の方を直す（Notion で直しても次の同期で上書きされる）

## はじめに一度だけやること

1. https://www.notion.so/profile/integrations で連携（インテグレーション）を作り、トークンをコピーする
2. Notion で「GitHubドキュメント（Nishiki）」ページを開き、右上の「…」→「接続」から 1 の連携を追加する
3. GitHub のリポジトリの Settings → Secrets and variables → Actions で、`NOTION_TOKEN` という名前でトークンを登録する

## ファイルとページの対応

`config.json` の `pages` に書く。ここに無い `docs/*.md` を足した場合は、親ページの下にファイル先頭の `#` 見出しと同じ名前のページを探し、無ければ作る。作られたページの ID はログに出るので、`config.json` に足しておくと確実。

## 手元で試す

```bash
cd tools/notion-sync
npm ci
npm run check                                   # Notion に書かずに変換だけ確かめる
NOTION_TOKEN=... node sync.mjs docs/仕様書.md   # 1 ファイルだけ写す
```

## 変換のきまり

- ファイル先頭の `#` 見出しはページのタイトルなので、本文からは外す
- ほかの md への相対リンクは、対応する Notion ページへのリンクにする（対応が無ければ GitHub へのリンク）
- 相対パスの画像は GitHub の raw URL にする
- ページの先頭に、元ファイルへのリンクと同期したコミットを書いたお知らせを付ける
