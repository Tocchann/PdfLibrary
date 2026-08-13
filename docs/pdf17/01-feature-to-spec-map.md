# PDF 1.7 機能分解マップ（編集/レンダリングライブラリ向け）

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

## 3. API 境界（.NET 10+）

| 層 | 役割 | 依存 |
|---|---|---|
| Core (`PdfLibrary.Core`) | パーサ、オブジェクトモデル、編集、保存 | .NET 10+ |
| Extensions (`PdfLibrary.Extensions.Signing`) | 署名準備・署名辞書操作 | Core + 暗号プロバイダ抽象 |
| Host adapters | WPF/WinForms 向け利便API | UI固有型への薄い変換のみ |

## 4. レンダリング機能マップ

| Feature ID | 機能 | 主オブジェクト/キー | 参照章（ISO 32000-1） | 入力 | 出力 | 失敗条件 |
|---|---|---|---|---|---|---|
| RENDER-CTX-001 | 描画コンテキスト構築 | Graphics state stack, CTM, clipping path | 8.4, 8.5, 8.6 | ページ辞書、初期リソース、MediaBox | 初期化済み描画コンテキスト | 初期状態不整合、スタック破損 |
| RENDER-PATH-001 | パス描画 | Path operators (`m`,`l`,`c`,`h`,`re`,`S`,`f`,`B`) | 8.5.2, 8.5.3 | コンテンツストリーム演算子列 | パス描画結果 | 演算子列破損、塗り/線画状態不整合 |
| RENDER-TEXT-001 | テキスト描画 | Text state, text matrix, font resource | 9.2, 9.3, 9.4 | フォント辞書、文字列演算子列 | テキストレイアウト結果 | フォント未解決、テキスト行列不正 |
| RENDER-COLOR-001 | 色空間・色設定 | `/ColorSpace`, stroking/non-stroking color | 8.6.3, 8.6.4 | 色空間定義、色演算子 | 色適用済み描画状態 | 未対応色空間、成分数不一致 |
| RENDER-XOBJ-001 | XObject 描画 | `/XObject`, Form XObject, Image XObject | 8.8, 8.9 | XObject辞書、参照名 | 合成済み描画結果 | 参照切れ、再帰深度超過 |
| RENDER-PAGE-001 | ページレンダリング統合 | Content streams, resources, transparency group | 7.8.3, 8.4, 11 | ページオブジェクト | ページ画像（ラスタライズ結果） | リソース解決失敗、描画中断 |

## 5. レンダリング実装優先順位

| 優先 | 対象 Feature ID | 理由 |
|---|---|---|
| Wave 5 | RENDER-CTX-001, RENDER-PATH-001 | 最小の図形描画パイプラインを先に成立させるため |
| Wave 6 | RENDER-TEXT-001, RENDER-COLOR-001 | 業務文書の可読性に直結するテキストと色再現を強化するため |
| Wave 7 | RENDER-XOBJ-001, RENDER-PAGE-001 | 画像/フォームXObjectとページ全体統合を最終段階で安定化するため |
