# PDF 1.7 機能分解マップ（編集ライブラリ向け）

## 1. コア編集機能マップ

| Feature ID | 機能 | 主オブジェクト/キー | 参照章（ISO 32000-1） | 入力 | 出力 | 失敗条件 |
|---|---|---|---|---|---|---|
| DOC-EDIT-001 | 文書情報編集 | Trailer, `/Info`, Metadata stream | 7.5.5, 14.3.2, 14.3.3 | 更新キー集合 | 更新済み文書 | 文字列型不正、暗号制約 |
| PAGE-EDIT-001 | ページ挿入/削除/移動 | `/Catalog`, `/Pages`, `/Kids`, `/Count` | 7.7.2, 7.7.3 | 操作対象ページと順序 | 再構成Page Tree | 親子参照不整合、Count不一致 |
| ANNO-EDIT-001 | 注釈追加/更新/削除 | `/Annots`, Annotation dictionaries, `/Subtype` | 12.5.2, 12.5.6 | 注釈DTO | 更新済みページ | Rect不正、未対応Subtype処理不備 |
| BOOKMARK-001 | しおり読み書き | `/Outlines`, outline items, destination | 12.3.3 | ツリー構造 | アウトライン更新 | 参照切れ、親子リンク不正 |
| FORM-001 | AcroForm編集 | `/AcroForm`, field dictionaries, widget annot | 12.7.2, 12.7.3, 12.5.6.19 | フィールド定義/値 | フィールド更新 | 型不一致、外観更新漏れ |
| SIGN-001 | 署名フィールド管理 | Signature field, `/V`, `/ByteRange`, `/Contents` | 12.7.4.5, 12.8 | 署名対象設定 | 署名準備済みPDF | ByteRange計算不整合 |
| FILE-ATTACH-001 | 添付ファイル編集 | File Spec, Embedded file stream | 7.11.3, 7.11.4, 12.5.6.15 | ファイルと属性 | 埋め込み更新 | ファイル仕様不正 |
| SAVE-INC-001 | 増分保存 | xref/trailer/prev chain | 7.5.4, 7.5.5, 7.5.6, 7.5.8 | 変更オブジェクト集合 | 追記保存ファイル | xref破損、Prev連鎖不整合 |

## 2. 実装優先順位

| 優先 | 対象 Feature ID | 理由 |
|---|---|---|
| Wave 1 | DOC-EDIT-001, PAGE-EDIT-001, SAVE-INC-001 | 最小の「編集して保存できる」基盤 |
| Wave 2 | ANNO-EDIT-001, BOOKMARK-001 | デスクトップ業務アプリで需要が高い |
| Wave 3 | FORM-001, FILE-ATTACH-001 | 機能拡張として自然に接続可能 |
| Wave 4 | SIGN-001 | PKI連携・相互運用検証コストが高いため段階投入 |

## 3. API 境界（.NET Standard 2.0）

| 層 | 役割 | 依存 |
|---|---|---|
| Core (`PdfLibrary.Core`) | パーサ、オブジェクトモデル、編集、保存 | .NET Standard 2.0 のみ |
| Extensions (`PdfLibrary.Extensions.Signing`) | 署名準備・署名辞書操作 | Core + 暗号プロバイダ抽象 |
| Host adapters | WPF/WinForms 向け利便API | UI固有型への薄い変換のみ |
