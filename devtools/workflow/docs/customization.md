# Workflow Customization

`devtools/workflow/` の設定は、大きく `workflow JSON` と `prompt YAML` に分かれます。ここでは archetype の再利用、オプションの上書き、変数展開、マージルールなどの細かなカスタマイズ方法をまとめます。

## 役割分担

- workflow JSON: step 構成、Cursor CLI オプション、共通変数、継承関係を定義する
- prompt YAML: task ごとの prompt 本文、run 名、task 固有変数を定義する

基本方針として、共通の step 構造は `workflows/*.json` に寄せ、task ごとの差分は `prompt_configs/*.yaml` に寄せると再利用しやすくなります。

## Workflow JSON の最小構成

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

主な項目:

- `name`: workflow 名
- `run_name`: 実行結果の保存ディレクトリ名。未指定なら `name` を使います
- `description`: 説明文
- `variables`: workflow 全体で共有するテンプレート変数
- `defaults`: step 共通の Cursor CLI オプション
- `steps`: 実行順序と step ごとの差分設定
- `extends`: 単一の親 JSON を読み込んでから現在ファイルを重ねる
- `compose`: fragment JSON を配列順に重ねる

## Prompt YAML の最小構成

```yaml
run_name: example-repo-survey
variables:
  repo_name: AiChatCLI
steps:
  repo-survey:
    prompt: |
      Survey `${repo_name}` and summarize the important files.
```

主な項目:

- `run_name`: 出力先ディレクトリ名。未指定時は YAML ファイル名
- `variables`: workflow 全体で使う task 固有変数
- `steps.<name>.prompt`: 対応する step の prompt 本文
- `steps.<name>.variables`: その step にだけ渡す変数

`steps` の key は workflow 側の step 名と一致している必要があります。たとえば `aichatcli-implement-feature.json` を使うなら、YAML 側も `implement`、`review`、`apply-review-fixes` を使います。

## 継承と compose

複数の archetype を作るときは、共通 defaults と共通 step を分割しておくと管理しやすくなります。

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

おすすめの分割方針:

1. `defaults` 用 JSON で read-only と write-enabled の基本方針を分ける
2. fragment JSON で `repository-survey`、`plan-approach`、`implement`、`review`、`apply-review-fixes` などの共通 step を持つ
3. entry workflow JSON は archetype の並び順と名前だけを定義する
4. task ごとの差分は `prompt_configs/*.yaml` に寄せる

`extends` / `compose` で読み込まれる fragment JSON は、`name` や `steps` を省略できます。fragment 単体は `validate` / `run` の対象ではなく、最終 workflow に合成して使います。

## マージ優先順位

workflow 定義のマージ順:

1. `extends`
2. `compose` の先頭から末尾
3. 現在の workflow JSON
4. prompt YAML
5. CLI の `--var key=value`

マージルール:

- スカラー値は後勝ちです
- `variables` は key 単位で後勝ちです
- `defaults` はフィールド単位で後勝ちです
- `steps` は `name` 単位で置換し、同名でなければ末尾に追加します

## 変数の優先順位

テンプレートに渡される変数は次の順で積み上がります。

1. 組み込み変数
2. workflow 定義から得られた `variables`
3. prompt YAML の `variables`
4. CLI の `--var key=value`
5. workflow JSON の `steps[].variables`
6. prompt YAML の `steps.<name>.variables`

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
- `$${name}` と書くとリテラルの `${name}` を出力できます
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
- `prompt`: JSON 側に直接 prompt を持たせたい場合に使う

## パス解決

- `extends` と `compose` の参照先は、その JSON ファイル自身の位置を基準に解決されます
- `workspace` と `cwd` も、それぞれその項目を書いた JSON ファイル基準で解決されます
- 出力先は常に自動決定され、手動で `output_file` を指定することはできません

`output_file` は `devtools/workflow/runs/<run_name>/<index>-<step>.<ext>` になります。

## 典型的なカスタマイズ例

### 1. task ごとに prompt だけ差し替える

同じ `workflows/aichatcli-implement-feature.json` を使いながら、`prompt_configs/aichatcli-implement-chat-history-logging.yaml` や `prompt_configs/aichatcli-implement-prompt-config.yaml` のように YAML を切り替えます。

### 2. review step だけ read-only にする

write-enabled defaults を継承した workflow でも、`review` step 側で `mode: "ask"`、`force: false`、`yolo: false` を設定すれば、その step だけ安全側へ戻せます。

### 3. 実装前に調査 step を追加する

`aichatcli-implement-feature.json` の代わりに、`survey` と `plan` fragment を compose に追加した `aichatcli-complex-delivery.json` を使います。

## 注意

- `resume_from_previous: true` は 1 個目の step では使えません
- `steps.<name>` に存在しない step 名を書くと prompt YAML 側でエラーになります
- 循環 `extends` / `compose` はエラーになります
- 破壊的変更を許す workflow でも、最初は `--dry-run` で確認してから本実行するのが安全です

日々の実行方法やサンプルコマンドは [`usage.md`](usage.md) を参照してください。内部アーキテクチャや主要モジュールの責務は [`architecture.md`](architecture.md) を参照してください。
