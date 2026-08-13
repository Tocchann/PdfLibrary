# PDFライブラリ実装専用 instruction（PDF 1.7）

## 1. 目的と適用範囲
- 本 instruction は、`docs/pdf17` の既存文書を実装・PR運用に直接適用するための実行ルールである。
- 対象は PDF 編集ライブラリ（構文、オブジェクトモデル、編集、保存、署名フィールド、添付）と、Wave 5 の最小レンダリング基盤とする。
- 仕様の一次情報は `docs/pdf17/00` から `03` の文書とし、矛盾時はそちらを優先する。

## 2. 実装スコープ固定
### 2.1 Must（P0）
- 7.3 基本オブジェクト
- 7.5 ファイル構造（xref/trailer/増分更新）
- 7.7 Catalog/Page Tree/Name Tree
- 12.5 注釈
- 12.3.3 アウトライン
- 12.7 フォーム/署名フィールド
- 12.8 電子署名（PDF構造側）

### 2.2 Should（P1）
- 7.6 暗号化/セキュリティハンドラ制約
- 14.3 Metadata/Info
- 7.11.4 Embedded File Streams

### 2.3 Later（初期範囲外）
- 9, 10, 11（テキスト描画、色空間高度対応、XObject/ページ合成）
- 13（マルチメディア/3D）
- 14.7+（Tagged PDF 詳細）

## 3. 機能実装ルール（Feature ID準拠）
- 実装単位は `01-feature-to-spec-map.md` の Feature ID で管理する。
- Wave 優先度は以下を維持する。  
  Wave 1: `DOC-EDIT-001` `PAGE-EDIT-001` `SAVE-INC-001`  
  Wave 2: `ANNO-EDIT-001` `BOOKMARK-001`  
  Wave 3: `FORM-001` `FILE-ATTACH-001`  
  Wave 4: `SIGN-001`  
  Wave 5: `RENDER-CTX-001` `RENDER-PATH-001`
- 1つの PR で複数 Feature ID を扱う場合、相互依存があるものだけに限定する。

## 3.1 Wave 5 レンダリング実装境界
- Wave 5 は `q` `Q` `cm` `m` `l` `c` `h` `re` `S` `f` `B` のみを対象とする。
- `/Contents` は単一 stream、stream 参照、stream 配列の順序結合をサポートする。
- `MediaBox` は必須入力とし、未設定ページは明示エラーにする。
- 未対応演算子は黙殺せず明示エラーにする。
- テキスト描画、色空間、XObject、透明グループ、クリッピング規則の詳細評価は Wave 6 以降へ送る。

## 4. オブジェクトモデル・編集規約
- `02-object-model-and-edit-rules.md` の最小モデル（Catalog/PageTree/Page/Annot/Outline/AcroForm/Sig）を必須採用する。
- 編集開始時に変更対象の間接オブジェクト ID 集合を確定する。
- 新規オブジェクトは未使用番号を採番し、世代番号は 0 を既定とする。
- 既存オブジェクト更新時は原則として世代番号を維持し、xref は追記で表現する。
- 保存時は `xref + trailer + startxref + %%EOF` を必ず再構成する。
- 増分更新時は trailer の `/Prev` を直前 xref に接続する。

## 5. 保存・署名・暗号化・未対応仕様の方針
- 保存方式の既定は増分更新とし、フルリライトは初期実装の必須要件に含めない。
- 署名機能は責務分離を厳守する。  
  ライブラリ責務: 署名辞書、ByteRange プレースホルダ、増分更新。  
  外部責務: CMS 生成、証明書検証、タイムスタンプ。
- 暗号化文書の編集はポリシー適合を必須とし、未対応方式は明示エラーにする。
- 未対応注釈 Subtype は破棄せず保持し、透過保存を原則とする。

## 6. 保存前チェック（Check ID準拠）
- 保存処理には以下チェックを必須適用する。  
  `CHK-001` Catalog -> Pages 参照解決  
  `CHK-002` Page Tree Count 再計算一致  
  `CHK-003` 間接参照解決可能性  
  `CHK-004` 注釈 `/Rect` と必須キー妥当性  
  `CHK-005` 署名 ByteRange 範囲非重複  
  `CHK-006` 暗号化文書ポリシー適合
- `CHK-002` は自動再計算後に再検証し、他チェックの失敗は保存/署名を中止する。

## 7. PR運用ルール
- すべての実装 PR は対象 Feature ID と Check ID を明記する。
- 未対応仕様は「未対応理由」と「将来対応条件」を PR 本文に必ず記載する。
- 実装コメントを仕様の正とせず、仕様判断は `docs/pdf17` に追記して管理する。

## 8. PR記載テンプレート
以下を PR 本文に記載する。

```text
## 対応範囲
- Feature ID: <ID, ...>
- 参照章: <ISO 32000-1 の章番号>

## 保存前チェック
- Check ID: <CHK-001, CHK-002, ...>
- 結果: <pass/fail と理由>

## 仕様上の判断
- 署名済みPDFへの扱い: <方針>
- 暗号化PDFへの扱い: <方針>
- 未対応Subtypeの扱い: <方針>

## 未対応事項
- 項目:
- 未対応理由:
- 将来対応条件:
```

## 9. 相互運用確認
- 実装完了時は `03-roadmap-and-checklist.md` の相互運用観点（Acrobat/Edge/Chrome/業務PDF）に沿って確認する。
- 相互運用差異が出た場合、再現条件を文書化し、Feature ID と Check ID に紐付けて記録する。
