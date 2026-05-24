---
name: create-workflow
description: How to use or edit workflow to support AiChatCLI development
---

# ワークフロー

あなたは、あなた自身が作業するのでなく、極力ワークフローによる自動化によって、作業する仕組みを作る役割があります。
（もちろん、そのワークフロー自体を作るためにあなたが作業するのは問題ありません。）

これは、Dotnet CLIアプリを支援する仕組みであり、全く別のワークフローです。
**Cursor CLIをDotnet側に組み込んだり、既存のDotnet CLI機能をPython側に組み込んだりすることはありません。**

## あるべき姿

- cursor CLI を基本的に利用すべきです
- 単発の実行では意味が薄いので、連続実行できるような仕組みを作る必要があります
    - 連続実行や微調整を行うためのCLIラッパーとしてPythonを利用します
    - 固定的なワークフローではなく、設定に応じて柔軟に変更できる必要があります
- SubAgents, Skills, Rules などを上手く活用して、無理に全てをスクリプトでやろうとしないでください
(最終的には拡張性を考えると、結局スクリプトにどんどんよると思われるが、最初の方は無理にスクリプトでやる必要はない)
- カスタムツール・MCPサーバーを作って、ワークフローの流れをスムーズにできないか考慮してください
- 最初は基本的にYOLOモードを使って、徐々に後から危ないツール・不要なツールを不許可にしていく

## CURSOR CLI 参考

コマンドの使い方
https://cursor.com/docs/cli/reference/parameters

許可の仕方
https://cursor.com/docs/cli/reference/permissions

## 現状

`devtools\workflow\README.md`を参照