# 実装前ロードマップと準拠チェックリスト

## 1. 実装ロードマップ（段階導入）

| マイルストーン | 対象 | 完了条件 |
|---|---|---|
| M1 | 構文/オブジェクト/保存基盤 | 7.3, 7.5 の読み書きと増分保存が成立 |
| M2 | ドキュメント編集コア | ページ編集・文書情報更新・しおり更新が成立 |
| M3 | 注釈/フォーム | 主要注釈SubtypeとAcroForm更新が成立 |
| M4 | 署名・添付・運用補助 | 署名フィールド処理、添付編集、運用ガイド整備 |

## 2. 仕様準拠チェック（抜粋）

| 分類 | チェック項目 | 参照章 | ステータス |
|---|---|---|---|
| Syntax | 文字列/名前/辞書/ストリームの正規化 | 7.3 | Pending |
| Save | xref/trailer/startxref/EOF 正当性 | 7.5.4, 7.5.5 | Pending |
| Save | 増分更新の `/Prev` 連鎖正当性 | 7.5.6 | Pending |
| Structure | Catalog/Page Tree の参照整合性 | 7.7.2, 7.7.3 | Pending |
| Bookmark | Outline 階層整合性 | 12.3.3 | Pending |
| Annotation | Subtype別の必須キー検証 | 12.5.6 | Pending |
| Form | フィールド木・Widget連携 | 12.7 | Pending |
| Signature | ByteRange/Contents の整合性 | 12.8 | Pending |
| Metadata | Info/XMP 同期方針 | 14.3 | Pending |

## 3. 相互運用チェック（実装後）

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
