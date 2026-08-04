# 実装前ロードマップと準拠チェックリスト

## 1. 実装ロードマップ（段階導入）

| マイルストーン | 対象 | 完了条件 | 実装Wave |
|---|---|---|---|
| M1 | 構文/オブジェクト/保存基盤 | 7.3, 7.5 の読み書きと増分保存が成立 | Wave 1 ✅ |
| M2 | ドキュメント編集コア | ページ編集・文書情報更新・しおり更新が成立 | Wave 1, 2 ✅ |
| M3 | 注釈/フォーム | 主要注釈SubtypeとAcroForm更新が成立 | Wave 2, 3 ✅ |
| M4 | 署名・添付・運用補助 | 署名フィールド処理、添付編集、運用ガイド整備 | Wave 3, 4 ✅ |

## 2. 仕様準拠チェック（抜粋）

| 分類 | チェック項目 | 参照章 | ステータス |
|---|---|---|---|
| Syntax | 文字列/名前/辞書/ストリームの正規化 | 7.3 | 実装済み |
| Save | xref/trailer/startxref/EOF 正当性 | 7.5.4, 7.5.5 | 実装済み |
| Save | 増分更新の `/Prev` 連鎖正当性 | 7.5.6 | 実装済み |
| Structure | Catalog/Page Tree の参照整合性 | 7.7.2, 7.7.3 | 実装済み |
| Bookmark | Outline 階層整合性 | 12.3.3 | 実装済み |
| Annotation | Subtype別の必須キー検証 | 12.5.6 | 実装済み |
| Form | フィールド木・Widget連携 | 12.7 | 実装済み |
| Signature | ByteRange/Contents の整合性 | 12.8 | 実装済み |
| Metadata | Info/XMP 同期方針 | 14.3 | 実装済み |

### Wave 4 SIGN-001 実装詳細

**ライブラリ責務（実装済み）**:
- 署名フィールド（`/FT /Sig`）と Widget アノテーションの構築
- `/ByteRange` プレースホルダ付き増分保存
- `/Contents` プレースホルダへの CMS バイト列埋め込み
- `PdfHexString` 型（`<HEX>` 形式）のパース・書き込み

**外部責務（ライブラリ対象外）**:
- CMS/PKCS#7 の生成・検証（`ISignatureProvider` で抽象化）
- 証明書チェーン検証・失効確認（OCSP/CRL）
- タイムスタンプ（RFC 3161）
- 長期署名（PAdES）

**プロジェクト**: `PdfLibrary.Extensions.Signing`（`PdfLibrary.Core` に依存）

### XMP Metadata 実装詳細

**実装済み（`PdfLibrary.Core`）**:
- `PdfDocument.GetXmpMetadata()` — `/Catalog/Metadata` ストリームのバイト列を返す
- `PdfDocument.SetXmpMetadata(byte[])` — XMP XML バイト列を Metadata ストリームとして設定
- `PdfDocument.SyncXmpFromInfo()` — `/Info` 辞書から最小限の XMP パケットを自動生成して設定
- `PdfDocumentReader` — 既存 PDF の `/Catalog/Metadata` 読み込み

**XMP 自動同期の方針**:
- 自動同期（`SetInfo` 時に自動で XMP も更新）は実装しない
- 明示的に `SyncXmpFromInfo()` を呼ぶことで同期する設計（過剰な自動処理を避ける）

**未対応**:
- XMP のパース・編集（ライブラリは raw バイト列で扱う）
- PDF 日付文字列（`D:YYYYMMDD...`）の ISO 8601 への変換
- XMP Rights Management / Dublin Core 以外の高度なスキーマ



| 対象 | 観点 |
|---|---|
| Adobe Acrobat Reader | 注釈表示・しおり遷移・署名検証 |
| Microsoft Edge PDF | 基本表示、注釈互換、添付ファイル認識 |
| Chrome PDF Viewer | ページ再構成後の表示整合 |
| 既存業務PDF（社内サンプル） | 暗号化/フォーム混在時の回帰確認 |

## 4. 成果物の運用ルール

1. 仕様変更/解釈更新は本フォルダに追記し、実装側コメントではなく文書を正とする。  
2. 各実装PRは、対応 Feature ID と Check ID を必ず紐付ける。  
3. 未対応仕様は「未対応理由」と「将来対応条件」を明記し、暗黙放置しない。
