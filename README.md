# Zen - Horse Race Prediction By ML.NET - 競馬予測システム開発プロジェクト
#ai-generates

## 1. プロジェクト概要
機械学習 (ML.NET) を用いて競馬の結果を予測するシステム。
Webスクレイピングによりデータを収集し、独自の予測モデルを構築・運用する。
ユーザーはWebインターフェースを通じてレースを指定し、予測結果を閲覧できる。

## 2. プロジェクト構成
本ソリューション (`ZenMLRace.sln`) は以下の5つのプロジェクトで構成されている。

### ZenMLRace.Core
*   **種類**: Class Library
*   **役割**: ドメイン層。システムの中心となるビジネスロジック、エンティティ定義、インターフェース、共通DTOを提供する。
*   **依存**: なし

### ZenMLRace.Infrastructure
*   **種類**: Class Library
*   **役割**: インフラストラクチャ層。データアクセス (Entity Framework Core)、外部サイトへのスクレイピング処理などの実装詳細を担当する。
*   **依存**: Core

### ZenMLRace.ML
*   **種類**: Class Library
*   **役割**: 機械学習層。ML.NETを用いたモデル定義、学習パイプライン、推論エンジンの実装を担当する。
*   **依存**: Core

### ZenMLRace.Worker
*   **種類**: Worker Service
*   **役割**: バックグラウンド処理。定期的なデータ収集ジョブや、予測リクエストの非同期処理を実行する。
*   **依存**: Core, Infrastructure, ML

### ZenMLRace.API
*   **種類**: ASP.NET Core Web API
*   **役割**: プレゼンテーション層（API）。フロントエンドからのリクエスト受け付け、認証、ジョブ投入などを行うシステムの入り口。
*   **依存**: Core, Infrastructure, ML
