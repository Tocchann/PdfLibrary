# PDF 1.7 編集ライブラリ向け読解ガイド

対象: ISO 32000-1:2008 (PDF 1.7)  
一次資料: `docs/PDF32000_2008.pdf`  
目的: 実装に必要な範囲を先に固定し、以後の設計・実装・テストを一本化する。

## 0. 参照の固定ルール

1. 仕様判断の一次情報は必ず `docs/PDF32000_2008.pdf` とする。
2. `docs/pdf17/01-feature-to-spec-map.md` から `04-implementation-instruction.md` は、すべてこの一次情報の要約・実装方針・運用ルールとして扱う。
3. 実装コメント、PR本文、コミットログにだけ現れる独自解釈は正としない。判断が必要な場合は本ガイドか下位文書を更新する。
4. 章番号や用語に揺れが出ないよう、以下の表を唯一の参照先として使う。

## 1. 優先読解スコープ（Must）

| 優先度 | 章 | 主題 | 実装での役割 |
|---|---|---|---|
| P0 | 7.3 | 基本オブジェクト（辞書/配列/文字列/ストリーム） | すべての編集処理の基礎 |
| P0 | 7.5 | ファイル構造（xref/trailer/増分更新） | 保存・追記・整合性維持 |
| P0 | 7.7 | Catalog/Page Tree/Name Tree | 文書構造編集・ページ再編 |
| P0 | 12.5 | Annotations | 注釈の読み書き・更新・削除 |
| P0 | 12.3.3 | Document Outline | しおり（アウトライン）編集 |
| P0 | 12.7 | Interactive Forms/Signature Field | フォーム編集・署名フィールド |
| P0 | 12.8 | Digital Signatures | 署名辞書・ByteRange・変換手法 |
| P1 | 7.6 | Encryption/Security Handlers | 暗号化PDF編集時の制約対応 |
| P1 | 14.3 | Metadata / Info Dictionary | 文書情報更新 |
| P1 | 7.11.4 | Embedded File Streams | 添付ファイル機能 |

## 2. 後回しスコープ（Should/Later）

| 区分 | 章 | 理由 |
|---|---|---|
| Later | 8, 9, 10, 11 | 描画・レンダリング中心（編集コアの初期範囲外） |
| Later | 13 | マルチメディア/3D（対象アプリの要求が出てから） |
| Later | 14.7+, Tagged PDF 詳細 | アクセシビリティ高度要件は第2段階 |

## 2.1 用語の固定

| 用語 | この文書での意味 |
|---|---|
| Catalog | 文書ルート。`/Type /Catalog` と `/Pages` を持つ。 |
| Page Tree | `/Pages` ツリー全体。`/Kids` と `/Count` を持つ。 |
| Annotation | ページにぶら下がる注釈辞書。`/Subtype` と `/Rect` を持つ。 |
| Outline | しおりツリー。`/Outlines` 配下の項目群。 |
| AcroForm | インタラクティブフォームのルート辞書。 |
| Signature Dictionary | 署名辞書。`/ByteRange` と `/Contents` を持つ。 |

## 2.2 実装範囲の固定

- 初期実装の正は `01-feature-to-spec-map.md` の Wave 1〜4 とする。
- Wave の並び順は変更しない。
- 例外的に先回り実装をする場合でも、PR 上は必ず対応 Feature ID を明記する。

## 3. 実装上の読解ルール

1. 章を読むときは「オブジェクト単位」で抜き出す（例: `/Page`, `/Annot`, `/Outlines`, `/AcroForm`）。
2. 各要素について「必須キー」「条件付きキー」「更新時の副作用」を同時記録する。
3. 読解メモは自由文ではなく、`01-feature-to-spec-map.md` の表に正規化する。
4. 同じ概念について別文書で表現を変えない。章番号、Feature ID、Check ID のいずれかで必ず相互参照する。

## 4. 初回読解で確定させる制約

- 増分更新のみで保存可能か（フルリライトはオプションにするか）。
- 署名済みPDFへの追記ポリシー（許容/拒否/警告）。
- 暗号化PDFの編集ポリシー（復号後編集か、非対応で明示エラーか）。
- 未対応注釈Subtypeの扱い（保持して透過保存するか）。

## 5. ぶれ防止の運用メモ

- 仕様判断に迷ったら、まず `docs/PDF32000_2008.pdf` の該当章を確認する。
- 下位文書の記述が一次資料と食い違った場合は、下位文書を修正して一次資料に合わせる。
- 章の解釈を固定したら、`01-feature-to-spec-map.md` と `04-implementation-instruction.md` の該当箇所も同時に整合させる。
