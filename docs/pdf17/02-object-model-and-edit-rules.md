# PDF 1.7 オブジェクトモデル草案と編集規約

## 1. 主要オブジェクトモデル（最小セット）

| Model ID | PDF要素 | 主キー | 必須制約 |
|---|---|---|---|
| OBJ-CATALOG | Document Catalog | `/Type /Catalog`, `/Pages` | ルートとして一意 |
| OBJ-PAGE-TREE | Page Tree Node | `/Type /Pages`, `/Kids`, `/Count` | `Count == 子孫Page総数` |
| OBJ-PAGE | Page Object | `/Type /Page`, `/Parent`, `/MediaBox` | Parent必須、継承解決可能 |
| OBJ-ANNOT | Annotation | `/Subtype`, `/Rect` | Subtypeごとの必須キー充足 |
| OBJ-OUTLINE | Outline Item | `/Title`, `/Parent`, (`/First`,`/Last` 任意) | 兄弟/親リンクの循環禁止 |
| OBJ-ACROFORM | AcroForm Dictionary | `/Fields` | フィールド木の整合性維持 |
| OBJ-SIG | Signature Dictionary | `/Filter`, `/SubFilter`, `/ByteRange`, `/Contents` | ByteRangeの順序と範囲妥当性 |

## 2. 編集トランザクション規約

1. 変更開始時に「変更対象間接オブジェクトID集合」を確定する。  
2. 新規オブジェクトは未使用番号を採番し、世代番号は既定 0。  
3. 既存更新オブジェクトは世代番号を適切に更新し、旧xrefを破壊しない。  
4. 保存時は `xref + trailer + startxref + %%EOF` を必ず追記再構成する。  
5. 増分更新時は trailer の `/Prev` を直前xrefへ接続する。  
6. クロスリファレンスストリーム採用時も非対応リーダ互換を意識する（7.5.8.4）。

## 3. 整合性チェック（保存前）

| Check ID | 内容 | 失敗時 |
|---|---|---|
| CHK-001 | Catalog -> Pages 参照解決 | 例外（保存中止） |
| CHK-002 | Page Tree Count 再計算一致 | 自動再計算後に再検証 |
| CHK-003 | すべての間接参照が解決可能 | 例外（保存中止） |
| CHK-004 | 注釈の `/Rect` と必須キー妥当性 | 対象注釈をエラー報告 |
| CHK-005 | 署名時 ByteRange の範囲非重複 | 例外（署名処理中止） |
| CHK-006 | 暗号化文書の編集ポリシー適合 | ポリシー違反エラー |

## 4. 署名機能の責務分離

- ライブラリ責務: PDF構造処理（署名辞書、ByteRangeプレースホルダ、増分更新）。  
- 外部責務: CMS生成、証明書検証、タイムスタンプ発行。  
- 境界は `ISignatureProvider` のような抽象インターフェースで分離する。
