#![20260404172242_1](https://github.com/user-attachments/assets/3dc962f0-2eb1-45e6-b949-a5295b0a1880)
 MGT2 Awards & Dev Prediction Overlay

An unofficial BepInEx plugin for **Mad Games Tycoon 2**.  
This mod provides an in-game overlay for development prediction and award analysis.

---

## 📌 Supported Version

This mod is developed and tested based on:

- **Mad Games Tycoon 2**
  - BUILD: `REL-2025.04.08A`
- **Realism Mod v4.2.0 (2026)**

Reference:
- https://steamcommunity.com/app/1342330/discussions/1/3199245644687317862/

⚠️ Other versions may not work correctly.

---

## 📌 Features

### 🎮 Development Prediction (F7)
- Predicts total review score for games in development
- Shows breakdown:
  - Gameplay
  - Graphics
  - Sound
  - Control
- Displays:
  - Distance to 80 score
  - Penalties (red)
  - Bonuses (cyan)
- Suggests improvement priorities

---

### 🏆 Awards Overlay (F8)
Displays current award candidates for the ongoing season:

- Graphics Award candidates
- Sound Award candidates
- Game of the Year (GOTY)
- Worst Game

---

### 📊 Company Rankings
Based on current season releases:

- Best Developer Award (Studio Points ranking)
- Publisher Award (Sales/Publisher points ranking)

---

### 🧾 Your Released Games
- Lists your games released in the current award period only

---

## 🎯 Controls

| Key | Function |
|-----|--------|
| F7 | Toggle development prediction overlay |
| F8 | Toggle awards overlay |
| F6 | Toggle compact mode |

---

## 📅 Award Period Logic

- Award season runs from:
  - **December (Week 1) → November (Week 4)**
- After awards (end of November):
  - New season starts immediately in December

---

## ⚙️ Requirements

- Mad Games Tycoon 2
- BepInEx 5.x

---

## 📥 Installation

1. Install BepInEx
2. Place the `.dll` file into:


Mad Games Tycoon 2/  

  
  └── BepInEx/
  
  
  └── plugins/　👈 Place the .dll file here


3. Start the game

---

## ⚠️ Disclaimer

- This is an **unofficial mod**
- Uses **reflection** to access internal game data
- May break after game updates or mod updates
- No game files are included
- Use at your own risk

---

## 🇯🇵 日本語説明

このMODは Mad Games Tycoon 2 用の非公式BepInExプラグインです。  
ゲーム内にオーバーレイを表示し、開発中ゲームの評価予測や受賞候補を確認できます。

---

## 📌 対応バージョン

本MODは以下の環境を基準に作成されています：

- Mad Games Tycoon 2
  - BUILD: `REL-2025.04.08A`
- Realism Mod v4.2.0 (2026)

参考:
- https://steamcommunity.com/app/1342330/discussions/1/3199245644687317862/

※上記以外のバージョンでは正常に動作しない可能性があります

---

### 主な機能

#### ■ 開発中ゲーム予測（F7）
- 総合レビュー予測
- 各要素（ゲームプレイ・グラフィック・サウンド・コントロール）の内訳
- 減点要素（赤）・加点要素（青）の表示
- 改善優先度の提示

---

#### ■ 受賞候補表示（F8）
- 映像賞
- サウンド賞
- GOTY
- ワースト

---

#### ■ 会社ランキング
- 最優秀開発賞（Studio Points）
- パブリッシャー賞

---

#### ■ 自社作品一覧
- 今期にリリースしたゲームのみ表示

---

### 操作

- F7：開発予測表示 ON/OFF  
- F8：受賞候補表示 ON/OFF  
- F6：簡易表示切替  

---

### 注意

- 非公式MODです
- ゲームやMODのアップデートで動作しなくなる可能性があります
- 内部データをリフレクションで取得しています
- 自己責任でご使用ください
