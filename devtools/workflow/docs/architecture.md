# Workflow Architecture

`devtools/workflow/` は、Cursor CLI を直接ラップする薄い Python CLI です。設定の読み込み、workflow 合成、prompt 変数展開、step 実行、成果物保存を責務ごとに分け、AiChatCLI 本体とは独立したツールとして保っています。

## 全体像

入力:

- workflow JSON
- prompt YAML
- CLI 引数 (`validate` / `run`, `--prompt-config`, `--var`, `--dry-run`)

出力:

- step ごとの標準出力
- step ごとのメタデータ JSON
- 必要時の標準エラー
- 後続 step へ引き継げる `chat_id`

典型的な実行フローは次の通りです。

1. `cli.py` が引数を解釈し、workflow JSON を読み込む
2. `workflow_loader.py` が `extends` / `compose` を解決し、workflow 定義を 1 個に合成する
3. `prompt_config.py` が prompt YAML を取り込み、task 固有 prompt と変数を重ねる
4. `validation.py` が step 構造と Cursor CLI オプションを検証する
5. `runtime.py` が各 step を順に準備し、Cursor CLI を実行する
6. `artifacts.py` が stdout / stderr / metadata を `runs/` に書き出す

## ディレクトリ構成

- `src/workflow_cli/`: 本体パッケージ
- `workflows/`: 再利用可能な workflow archetype と fragment
- `prompt_configs/`: task 別 prompt YAML
- `runs/`: 実行結果の出力先
- `tests/`: workflow loader、templating、validation の focused test

## 主要モジュール

### CLI 入口

- `src/workflow_cli/cli.py`: `validate` と `run` のサブコマンドを定義し、`--prompt-config`、`--var`、`--dry-run` を解釈します
- `pyproject.toml`: `workflow-cli = "workflow_cli.cli:main"` を console script として公開します

### 設定モデル

- `src/workflow_cli/models.py`: `WorkflowConfig`、`StepConfig`、`PromptConfig`、`CursorOptions` を dataclass で定義します
- `src/workflow_cli/options.py`: `defaults` と step override を読み込み、最終的な Cursor CLI 引数へ変換します

### workflow 合成

- `src/workflow_cli/workflow_loader.py`: `extends` と `compose` を再帰的に読み込み、`variables`、`defaults`、`steps` をルールに沿ってマージします
- `steps` は `name` 単位で扱い、同名 step は置換、それ以外は末尾追加になります
- `workspace` と `cwd` は、その項目を書いた JSON ファイル自身を基準に絶対パスへ正規化されます

### prompt 適用とテンプレート展開

- `src/workflow_cli/prompt_config.py`: prompt YAML を読み込み、workflow 側の step 名と突き合わせて prompt や変数を上書きします
- `src/workflow_cli/templating.py`: `${name}` 形式の置換を行い、未定義変数が残っていると明示的にエラーにします
- `src/workflow_cli/step_support.py`: 組み込み変数の組み立て、出力先決定、prompt レンダリング、`resume_from_previous` の準備を担当します

### 実行と成果物保存

- `src/workflow_cli/runtime.py`: step ごとに `PreparedStep` を作り、Cursor CLI 実行、`chat_id` 抽出、失敗時の停止判定までをまとめます
- `src/workflow_cli/cursor_cli.py`: シェル文字列ではなく引数配列で Cursor CLI 呼び出しを構築し、`subprocess.run` で実行します
- `src/workflow_cli/artifacts.py`: stdout、stderr、metadata をファイルへ保存します
- `src/workflow_cli/cursor_output.py`: Cursor CLI の出力から `chat_id` 相当を抽出し、後続 step の `--resume` に使える形へ寄せます

### 検証

- `src/workflow_cli/validation.py`: prompt 未設定、先頭 step での `resume_from_previous`、サポート外オプション値などを検出します
- `tests/test_workflow_loader.py`: 継承・compose・パス解決周りを検証します
- `tests/test_templating_and_validation.py`: 変数展開と validation の振る舞いを検証します

## 実行時データの流れ

各 step の準備では、workflow と step の設定から次を確定します。

- 最終 Cursor CLI オプション
- `cwd` と `workspace`
- `runs/<run_name>/` 配下の出力先
- prompt に埋め込む変数
- 必要なら前 step から引き継ぐ `resume_chat_id`

実行後は次の順で情報が保存されます。

1. stdout をメイン出力ファイルへ書く
2. stderr があれば別ファイルへ書く
3. 実行コマンド、`cwd`、`workspace`、`output_format`、終了コード、経過秒数、`chat_id` を metadata JSON へ書く
4. 取得した `chat_id` を次 step の `resume_from_previous` 用に保持する

## アーキテクチャ上の意図

- AiChatCLI 本体とは独立した補助ツールとして保ち、.NET 側の runtime 詳細を持ち込まない
- archetype は JSON で再利用し、task ごとの差分は YAML へ逃がして変更コストを下げる
- Windows を含む環境で安定させるため、CLI 呼び出しはシェル文字列ではなく引数配列で構築する
- 出力とメタデータを step 単位で残し、再現性と監査性を持たせる

## 読み始める順番

- 使い方を知りたい: [`usage.md`](usage.md)
- JSON/YAML をどう設計するか知りたい: [`customization.md`](customization.md)
- 実装を追いたい: `cli.py` -> `workflow_loader.py` -> `prompt_config.py` -> `step_support.py` -> `runtime.py`
