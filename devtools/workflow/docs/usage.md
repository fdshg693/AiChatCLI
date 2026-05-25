# Workflow Usage

`devtools/workflow/` の Python CLI は、workflow JSON と markdown prompt を入力にして `cursor-agent` を複数 step 実行します。ここでは、日々の利用手順とコマンドの使い分けに絞って説明します。

## セットアップ

PowerShell 例:

```powershell
cd devtools/workflow
py -3 -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -e .
```

editable install 後は `python -m workflow_cli ...` と `workflow-cli ...` のどちらでも実行できます。このガイドでは既存のサンプルに合わせて `python -m workflow_cli` 表記を使います。

## 基本コマンド

workflow を検証する:

```powershell
cd devtools/workflow
python -m workflow_cli validate workflows/aichatcli-dev-support.json
```

実行コマンドを表示するだけのドライラン:

```powershell
cd devtools/workflow
python -m workflow_cli run workflows/aichatcli-dev-support.json --dry-run
```

実際に実行する:

```powershell
cd devtools/workflow
python -m workflow_cli run workflows/aichatcli-dev-support.json
```

実行時に変数を上書きする:

```powershell
cd devtools/workflow
python -m workflow_cli run workflows/aichatcli-dev-support.json --var focus_area=prompt handling
```

## コマンドの考え方

- `validate`: workflow JSON、markdown prompt 参照、step ごとのオプション妥当性を確認します
- `run`: step を順番に実行し、結果を `runs/` に保存します
- `--var key=value`: workflow 変数を実行時に上書きまたは追加します
- `--dry-run`: 実行せず、各 step で組み立てられる command、`cwd`、出力先だけ確認します

`run` では各 step に `prompt` が必要です。prompt 内の `${file:name.md}` は `prompt_configs/` 配下の markdown ファイル内容に置き換わり、その後 `${name}` 変数が展開されます。

## よく使う実行パターン

調査と計画だけ回したい:

```powershell
cd devtools/workflow
python -m workflow_cli run workflows/aichatcli-dev-support.json
```

実装、レビュー、レビュー反映を回したい:

```powershell
cd devtools/workflow
python -m workflow_cli validate workflows/aichatcli-implement-feature.json
python -m workflow_cli run workflows/aichatcli-implement-feature.json --var task_summary="add chat history logging support"
```

複雑タスク向けに調査からレビュー反映まで一気に回したい:

```powershell
cd devtools/workflow
python -m workflow_cli run workflows/aichatcli-complex-delivery.json --var task_summary="design and implement chat history logging"
```

## 出力の見方

各 step は `devtools/workflow/runs/<run_name>/` に出力されます。

- 標準出力: `<index>-<step>.<ext>`
- メタデータ: `<index>-<step>.<ext>.meta.json`
- 標準エラー: エラーがある場合だけ `<index>-<step>.<ext>.stderr.txt`

`<ext>` は `output_format` に応じて切り替わります。

- `text`: `.md`
- `json`: `.json`
- `stream-json`: `.ndjson`

`run_name` は workflow JSON の `run_name`、それもなければ `name` が使われます。prompt で使う task 固有変数は `variables/*.json` から読み込まれます。

## サンプル archetype の使い分け

- `workflows/aichatcli-dev-support.json`: `ask` ベースの read-only 調査向け
- `workflows/aichatcli-implement-feature.json`: 実装、レビュー、レビュー反映の定型向け
- `workflows/aichatcli-complex-delivery.json`: 事前調査や方針整理を含む複雑タスク向け

`aichatcli-implement-feature.json` と `aichatcli-complex-delivery.json` は書き込みを許可します。まず `--dry-run` で command と step 順序を確認してから本実行するのが安全です。

## 補足

- task 固有値を一時的に変えたい場合は `--var key=value` で読み込み済みの variables を上書きできます
- `resume_from_previous: true` を使う step は、前 step から `chat_id` を引き継いで `--resume` 付きで実行されます
- Cursor CLI 側の権限設定や承認動作は、この runner だけでなく CLI 側設定にも依存します

workflow JSON と markdown prompt の詳細仕様、マージ優先順位、`defaults` や `steps` のカスタマイズ方法は [`customization.md`](customization.md) を参照してください。内部の責務分割や実行フローは [`architecture.md`](architecture.md) を参照してください。
