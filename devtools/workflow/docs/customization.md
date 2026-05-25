# Workflow Customization

`devtools/workflow/` の設定は、workflow JSON、`prompt_configs/*.md` の markdown prompt、`variables/*.json` の変数セットで構成されます。ここでは archetype の再利用、オプションの上書き、変数展開、マージルールなどの細かなカスタマイズ方法をまとめます。

## 役割分担

- workflow JSON: step 構成、Cursor CLI オプション、run 名、変数セット参照、継承関係を定義する
- markdown prompt: `prompt_configs/` 配下に prompt 本文を置き、workflow JSON の `prompt` から `${file:name.md}` で参照する
- variables JSON: `variables/` 配下に prompt へ渡す変数セットを置き、workflow JSON の `variables_file` からファイル名で参照する

基本方針として、step 構造は `workflows/*.json`、task 固有変数は `variables/*.json`、再利用したい prompt 本文は `prompt_configs/*.md` に置くと管理しやすくなります。

## Workflow JSON の最小構成

```json
{
  "schema_version": 1,
  "name": "example-workflow",
  "variables_file": "dev-support.json",
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
      "name": "repo-survey",
      "prompt": "${file:repository-survey.md}"
    }
  ]
}
```

主な項目:

- `name`: workflow 名
- `run_name`: 実行結果の保存ディレクトリ名。未指定なら `name` を使います
- `description`: 説明文
- `variables_file`: `variables/` 配下の変数セット JSON ファイル名
- `variables`: workflow 全体で共有するテンプレート変数。`variables_file` と同時には使えません
- `defaults`: step 共通の Cursor CLI オプション
- `steps`: 実行順序と step ごとの差分設定
- `extends`: 単一の親 JSON を読み込んでから現在ファイルを重ねる
- `compose`: fragment JSON を配列順に重ねる

## Markdown Prompt の最小構成

`prompt_configs/repository-survey.md`:

```markdown
Survey `${repo_name}` for ${task_summary}.

Summarize the important files and constraints.
```

主な項目:

- `steps[].prompt`: prompt 本文を直接書くか、`${file:name.md}` で markdown prompt を差し込む
- `variables_file`: `${repo_name}` や `${task_summary}` のような変数値を持つ JSON ファイルを指定する
- `run_name`: 出力先ディレクトリ名を workflow JSON 側で定義する

`${file:...}` は常に `devtools/workflow/prompt_configs/` を基準に解決されます。絶対パス、`..` で外へ出る参照、markdown 以外の拡張子は使えません。

## Variables JSON の最小構成

`variables/dev-support.json`:

```json
{
  "repo_name": "AiChatCLI",
  "task_summary": "support ongoing AiChatCLI development",
  "focus_area": "overall architecture",
  "acceptance_criteria": "produce a practical next-step plan"
}
```

`variables_file` は `variables/` 配下の JSON ファイル名だけを受け付けます。複数ファイルの結合、`variables_file` と workflow JSON 内の `variables` の併用、workflow JSON 側での上書きはサポートしません。一時的な実行時上書きには既存の `--var key=value` を使います。

## 継承と compose

`extends` / `compose` はサポートされていますが、同梱サンプルは用途が見えるように 3 つの self-contained JSON に絞っています。まずは `aichatcli-dev-support.json`、`aichatcli-implement-feature.json`、`aichatcli-complex-delivery.json` を直接読み、共通化が必要になったときだけ分割してください。

分割する場合の方針:

1. entry workflow JSON は実行対象として単体で読める名前と説明を持つ
2. 共通 prompt 本文は `prompt_configs/*.md` に置き、step の `prompt` から `${file:...}` で参照する
3. task 固有値は `variables/*.json`、一時的な上書きは CLI の `--var` に寄せる

`extends` / `compose` で読み込まれる JSON は、`name` や `steps` を省略できます。読み込み順とマージ規則は下の「マージ優先順位」を参照してください。

## マージ優先順位

workflow 定義のマージ順:

1. `extends`
2. `compose` の先頭から末尾
3. 現在の workflow JSON
4. CLI の `--var key=value`

マージルール:

- スカラー値は後勝ちです
- `variables` は key 単位で後勝ちです
- `variables_file` は単一ファイル参照です。inline `variables` とのマージは行いません
- `defaults` はフィールド単位で後勝ちです
- `steps` は `name` 単位で置換し、同名でなければ末尾に追加します

## 変数の優先順位

テンプレートに渡される変数は次の順で積み上がります。

1. 組み込み変数
2. workflow 定義または `variables_file` から得られた `variables`
3. CLI の `--var key=value`
4. workflow JSON の `steps[].variables`

組み込み変数:

- `${workflow_name}`
- `${run_name}`
- `${workflow_file}`
- `${workflow_dir}`
- `${step_name}`
- `${output_file}`
- `${cwd}`
- `${workspace}` (`workspace` が設定されている場合のみ)

テンプレート仕様:

- `${name}` 形式で置換します
- `${file:name.md}` 形式で `prompt_configs/name.md` の内容を差し込みます
- `$${name}` と書くとリテラルの `${name}` を出力できます
- `$${file:name.md}` と書くとリテラルの `${file:name.md}` を出力できます
- 未定義変数が残るとエラーになり、利用可能な変数一覧も表示されます

## Cursor CLI オプション

`defaults` と各 `steps[]` では次のオプションを扱えます。

- `command`: 実行コマンド。既定値は `cursor-agent`
- `subcommand`: 必要ならコマンド直後に差し込む追加サブコマンド
- `workspace`: `--workspace` に渡すパス
- `cwd`: プロセスの作業ディレクトリ
- `mode`: `ask` または `plan`
- `model`: Cursor CLI に渡すモデル名
- `output_format`: `text`、`json`、`stream-json`
- `print_mode`: `--print` を付けるか
- `force`: `--force` を付けるか
- `yolo`: `--yolo` を付けるか
- `sandbox`: `enabled` または `disabled`
- `approve_mcps`: `--approve-mcps` を付けるか
- `trust`: `--trust` を付けるか
- `extra_args`: 追加引数配列

step ごとに追加で使える制御項目:

- `continue_on_error`: 失敗しても次の step へ進む
- `resume_from_previous`: 前 step の `chat_id` を引き継いで `--resume` する
- `prompt`: 直接 prompt を書くか、`${file:...}` で markdown prompt を参照する

## パス解決

- `extends` と `compose` の参照先は、その JSON ファイル自身の位置を基準に解決されます
- `workspace` と `cwd` も、それぞれその項目を書いた JSON ファイル基準で解決されます
- 出力先は常に自動決定され、手動で `output_file` を指定することはできません

`output_file` は `devtools/workflow/runs/<run_name>/<index>-<step>.<ext>` になります。

## 典型的なカスタマイズ例

### 1. task ごとに prompt だけ差し替える

既存の `workflows/aichatcli-implement-feature.json` を使い、`--var task_summary=...` や `--var acceptance_criteria=...` で task 固有値を渡します。繰り返し使う task だけ、必要に応じて新しい variables JSON と workflow JSON として切り出します。

### 2. review step だけ read-only にする

write-enabled workflow でも、`review` step 側で `mode: "ask"`、`force: false`、`yolo: false` を設定すれば、その step だけ安全側へ戻せます。`aichatcli-implement-feature.json` と `aichatcli-complex-delivery.json` はこの形を採用しています。

### 3. 実装前に調査 step を追加する

`aichatcli-implement-feature.json` の代わりに、調査と計画 step を含む `aichatcli-complex-delivery.json` を使います。

## 注意

- `resume_from_previous: true` は 1 個目の step では使えません
- `${file:...}` が存在しない markdown ファイルを参照すると validation / run でエラーになります
- `variables_file` は `variables/` 直下の `.json` ファイル名だけを指定できます
- 循環 `extends` / `compose` はエラーになります
- 破壊的変更を許す workflow でも、最初は `--dry-run` で確認してから本実行するのが安全です

日々の実行方法やサンプルコマンドは [`usage.md`](usage.md) を参照してください。内部アーキテクチャや主要モジュールの責務は [`architecture.md`](architecture.md) を参照してください。
