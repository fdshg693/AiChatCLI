# Cursor Workflow

`devtools/workflow/` は、設定ファイルに沿って `cursor-agent` を複数 step 連続実行する Python 製の workflow runner です。

AiChatCLI 本体とは独立しており、workflow JSON と prompt YAML を組み合わせて、調査、計画、実装、レビューのような一連の Cursor CLI 実行を再利用しやすい形にまとめます。各 step の出力、メタデータ、標準エラーは `runs/` 配下へ保存されます。

## 何に使うか

- 読み取り専用の調査や設計を定型化する
- 実装、レビュー、レビュー反映の multi-step 実行を archetype 化する
- task ごとの差分を prompt YAML 側へ逃がし、workflow 構造を再利用する
- step ごとの生成物をファイルとして残し、後から追跡できるようにする

## ドキュメント

- 日々の使い方: [`docs/usage.md`](docs/usage.md)
- 設定とカスタマイズ: [`docs/customization.md`](docs/customization.md)
- 実装概要とアーキテクチャ: [`docs/architecture.md`](docs/architecture.md)
- リポジトリ全体の入口: [`../../README.md`](../../README.md)

## クイックスタート

PowerShell 例:

```powershell
cd devtools/workflow
py -3 -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -e .
python -m workflow_cli validate workflows/aichatcli-dev-support.json --prompt-config prompt_configs/aichatcli-dev-support.yaml
python -m workflow_cli run workflows/aichatcli-dev-support.json --prompt-config prompt_configs/aichatcli-dev-support.yaml --dry-run
```

`python` コマンドが通る環境なら `py -3` の代わりに `python` を使って構いません。editable install 後は `python -m workflow_cli ...` に加えて `workflow-cli ...` でも実行できます。

## 含まれている archetype

- `workflows/aichatcli-dev-support.json`: 調査 + 計画の read-only archetype
- `workflows/aichatcli-implement-feature.json`: 実装 + レビュー + レビュー反映の write-enabled archetype
- `workflows/aichatcli-complex-delivery.json`: 調査からレビュー反映まで含む複雑タスク向け archetype

どの archetype を選ぶか、オプションをどう渡すか、JSON/YAML をどうカスタマイズするか、内部でどのモジュールが何を担当しているかは、それぞれ詳細ドキュメントを参照してください。
