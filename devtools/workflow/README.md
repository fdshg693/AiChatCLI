# Cursor Workflow

`AiChatCLI` 本体とは独立した、Cursor CLI 用のバッチ実行ラッパーです。

このディレクトリの Python CLI は、設定ファイルに沿って `cursor-agent` を複数回連続実行し、各 step の出力とメタデータを保存します。最初の用途は `AiChatCLI` の開発・改修支援ですが、構成自体は他のリポジトリにも流用できるようにしてあります。

関連ドキュメント:

- リポジトリ全体の概要: [`../../README.md`](../../README.md)
- アプリ本体の入口: [`../../src/AiChatCLI/README.md`](../../src/AiChatCLI/README.md)
- アプリ開発ガイド: [`../../src/AiChatCLI/docs/development.md`](../../src/AiChatCLI/docs/development.md)

## ドキュメントの分担

- `src/AiChatCLI/README.md` はアプリ本体の入口とクイックスタートを説明します
- `src/AiChatCLI/docs/usage.md` と `src/AiChatCLI/docs/configuration.md` は、slash command、thread 履歴復元、ログ仕様、設定ファイルなどの user-facing な挙動を説明します
- `src/AiChatCLI/docs/development.md` は `AppPaths`、`ChatTurnPipeline`、`ConversationCodec`、`ThreadRecorder` など、アプリ側の主要な責務分割を説明します
- この README は `devtools/workflow/` 配下の Python runner と workflow archetype の使い方に集中します
- そのため、AiChatCLI 本体の runtime path 解決、thread replay、slash command 実装詳細はこの README に重複して持ち込みません

## セットアップ

PowerShell 例:

```powershell
cd devtools/workflow
py -3 -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -e .
```

`python` コマンドが使える環境なら `py -3` の代わりに `python` を使っても構いません。

## 実行

テンプレート単体の検証:

```powershell
cd devtools/workflow
python -m workflow_cli validate workflows/aichatcli-dev-support.json
```

テンプレート + prompt YAML の検証:

```powershell
cd devtools/workflow
python -m workflow_cli validate workflows/aichatcli-dev-support.json --prompt-config prompt_configs/aichatcli-dev-support.yaml
```

別の task-specific prompt config を使った実装系 template の検証:

```powershell
cd devtools/workflow
python -m workflow_cli validate workflows/aichatcli-implement-feature.json --prompt-config prompt_configs/aichatcli-implement-chat-history-logging.yaml
```

ドライラン:

```powershell
cd devtools/workflow
python -m workflow_cli run workflows/aichatcli-dev-support.json --prompt-config prompt_configs/aichatcli-dev-support.yaml --dry-run
```

本実行:

```powershell
cd devtools/workflow
python -m workflow_cli run workflows/aichatcli-dev-support.json --prompt-config prompt_configs/aichatcli-dev-support.yaml
```

変数上書き:

```powershell
cd devtools/workflow
python -m workflow_cli run workflows/aichatcli-dev-support.json --prompt-config prompt_configs/aichatcli-dev-support.yaml --var repo_name=AiChatCLI
```

## Workflow JSON

テンプレートの最小構成:

```json
{
  "schema_version": 1,
  "name": "example-workflow",
  "defaults": {
    "command": "cursor-agent",
    "workspace": "../../..",
    "cwd": "../../..",
    "mode": "ask",
    "output_format": "text",
    "print_mode": true,
    "force": false,
    "yolo": false
  },
  "steps": [
    {
      "name": "repo-survey"
    }
  ]
}
```

実行時の prompt YAML:

```yaml
run_name: example-repo-survey
variables:
  repo_name: AiChatCLI
steps:
  repo-survey:
    prompt: |
      Survey `${repo_name}` and summarize the important files.
```

継承とコンポジション:

```json
{
  "schema_version": 1,
  "extends": "./aichatcli-readonly-defaults.json",
  "compose": [
    "./aichatcli-survey-step.fragment.json",
    "./aichatcli-plan-step.fragment.json"
  ],
  "name": "aichatcli-dev-support",
  "description": "Read-only workflow archetype for repository survey and planning."
}
```

主な項目:

- `defaults.command`: Cursor CLI 実行コマンド。初期値は `cursor-agent`
- `defaults.workspace`: `--workspace` に渡すパス
- `defaults.cwd`: 実プロセスの作業ディレクトリ
- `defaults.mode`: `ask` または `plan`
- `defaults.output_format`: `text`, `json`, `stream-json`
- `defaults.force`: `--force` を付けるか
- `defaults.yolo`: `--yolo` を付けるか
- `extends`: 単一の親 workflow / fragment JSON を読み込んでから現在ファイルを重ねる
- `compose`: 複数の workflow / fragment JSON を順番に重ねる
- `steps[].prompt`: JSON 側に直接書きたい場合だけ使う。通常は prompt YAML 側で与える
- `variables` と `steps[].variables`: prompt 内の `${name}` を置換
- `run_name`: 実行結果の保存ディレクトリ名。通常は prompt YAML 側の `run_name` かファイル名が使われる
- `workflow-cli ... --prompt-config path.yaml`: 実行時 prompt と task 固有変数を読み込む
- `workflow-cli ... --var key=value`: 実行時に `variables` を上書き/追加

変数の最終優先順位:

1. 組み込み変数
2. workflow 定義から得られた `variables` (`extends` / `compose` / 現在ファイル)
3. prompt YAML の `variables`
4. CLI の `--var key=value`
5. workflow JSON の `steps[].variables`
6. prompt YAML の `steps.<name>.variables`

マージ優先順位:

1. `extends`
2. `compose` の先頭から末尾
3. 現在の workflow JSON
4. prompt YAML
5. CLI の `--var key=value`

マージルール:

- スカラー項目は後勝ちです
- `variables` は key 単位で後勝ちです
- `defaults` はフィールド単位で後勝ちです
- `steps` は `name` 単位で置換し、同名でなければ末尾に追加します

おすすめの分割方針:

1. `defaults` JSON で read-only / write-enabled の基本方針を持つ
2. fragment JSON で `repository-survey`, `plan-approach`, `implement`, `review`, `apply-review-fixes` のような共通 step を持つ
3. entry workflow JSON は archetype の並び順だけを定義する
4. task ごとの差分は `prompt_configs/*.yaml` の prompt 本文と変数に寄せる

`extends` / `compose` で読み込まれる fragment JSON は、`name` や `steps` を省略できます。fragment 単体は `validate` / `run` の対象ではなく、最終 workflow に合成して使います。

パスは「その項目を書いた JSON ファイル自身」を基準に解決されます。たとえば base workflow や fragment 内の `workspace`, `cwd` は、それぞれ定義元ファイル基準で扱われます。

循環継承・循環 compose はエラーになります。

## Prompt YAML

`prompt_config` は task ごとの実行内容を持つファイルです。テンプレート JSON とは分離し、通常はこちらに prompt 本文と task 固有変数を書きます。

主な項目:

- `run_name`: 出力先ディレクトリ名。未指定時は YAML ファイル名
- `variables`: workflow 全体で使う task 固有変数
- `steps.<name>.prompt`: step 名に対応する prompt 本文
- `steps.<name>.variables`: その step にだけ渡す変数

`steps` の key は、workflow archetype 側の step 名と一致している必要があります。たとえば `aichatcli-implement-feature.json` を使うなら、prompt config 側も `implement`, `review`, `apply-review-fixes` を使います。

テンプレート JSON に prompt がなくても、`run` 時に `--prompt-config` を渡せば実行できます。`validate` は prompt YAML なしでもテンプレート構造だけ確認できますが、実行可能性まで見たい場合は `--prompt-config` を付けてください。

利用できる組み込み変数:

- `${workflow_name}`
- `${run_name}`
- `${workflow_file}`
- `${workflow_dir}`
- `${step_name}`
- `${output_file}`
- `${cwd}`
- `${workspace}` (`workspace` が設定されている場合のみ)

テンプレート仕様:

- `${name}` 形式で置換します。変数名に使える文字は英数字と `_` です
- `$${name}` と書くと、展開せずにリテラルの `${name}` を出力できます
- 未定義変数が残っているとエラーになり、利用可能な変数一覧も表示されます

## 出力

各 step は次を保存します。

- 標準出力: `output_file`
- メタデータ: `output_file` に `.meta.json` を付けたファイル
- 標準エラー: エラーがあるときだけ `.stderr.txt`

`output_file` は常に Python 側で自動決定され、`devtools/workflow/runs/<run_name>/<index>-<step>.<ext>` になります。`<run_name>` は prompt YAML の `run_name`、未指定なら prompt YAML ファイル名、prompt YAML を使わない場合は workflow の `run_name` または `name` です。

`json` / `stream-json` の場合は raw 出力をそのまま保存します。`chatId` / `chat_id` / `conversationId` / `conversation_id` が返る形式なら、後続 step で `resume_from_previous: true` を使って `--resume` に引き継げます。

## サンプル Workflow

`workflows/aichatcli-dev-support.json` は、`workflows/aichatcli-readonly-defaults.json` を継承し、survey + planning の fragment を compose した読み取り専用 archetype です。`prompt_configs/aichatcli-dev-support.yaml` と組み合わせて次を順番に実行します。

1. リポジトリ構造と主要コンポーネントを整理する
2. 改修候補を開発タスクとして分解する

どちらも `text` 出力で、デフォルトでは書き込みを行いません。

`workflows/aichatcli-implement-feature.json` は、`workflows/aichatcli-write-defaults.json` を継承する 3 step の書き込み系 archetype です。task ごとの差分は prompt config 側へ逃がす前提で、次の共通 step 名を持ちます。

1. 実装
2. 要件レビュー
3. レビュー反映

レビュー step は template 側で `mode: "ask"` かつ `force: false` / `yolo: false` にしてあり、実装結果を読み取って差分だけ指摘する想定です。1 step 目と 3 step 目は継承したデフォルトにより書き込みを許可します。`prompt_configs/aichatcli-implement-chat-history-logging.yaml` や `prompt_configs/aichatcli-implement-prompt-config.yaml` のように、同じ archetype を task 別 YAML で使い分けます。

`workflows/aichatcli-complex-delivery.json` は、事前調査が必要な複雑タスク向けの 5 step archetype です。

1. 調査
2. 方針整理
3. 実装
4. レビュー
5. レビュー反映

`prompt_configs/aichatcli-complex-chat-history-logging.yaml` はそのサンプルで、複数ファイルにまたがる改修や、最初に調査と計画を挟みたいケース向けです。

## 注意

- 破壊的変更を許す workflow では `force: true` や `yolo: true` を使えますが、最初は読み取り中心の step から始めるのが安全です。
- Cursor CLI の権限設定は CLI 側設定にも依存します。必要に応じて `.cursor/cli.json` やグローバル設定を併用してください。
- Windows ではシェル文字列を組み立てず、Python から引数配列で実行する実装にしてあります。
