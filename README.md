# MathQuizLocker 🧮⚔️🛡️

MathQuizLocker is a fantasy-themed Windows utility that **locks the screen behind a mathematical battle**.

To continue using the PC, you must take up the mantle of a Knight and defeat monsters by solving multiplication problems (the “lille gangetabellen”). It is designed as a light-weight “focus / practice” lock – ideal for kids or self-discipline, not as a security product.

---

## ✨ Features

- ⚔️ **RPG Combat System**: Correct answers deal damage to monsters; incorrect answers result in the monster counter-attacking you.
- 📈 **Knight Progression**: Earn XP to level up your Knight. Your character sprite evolves through 10 distinct stages of armor as you progress.
- ⚖️ **Dynamic Penalty**: Damage taken for a wrong answer is exactly the **correct product** (e.g., failing $9 \times 9$ deals **81 damage**).
- 🔢 **Smart Math Engine**:
  - Automatically unlocks new rows of the multiplication table as you demonstrate mastery.
  - Supports commutative variety (e.g., both $1 \times 5$ and $5 \times 1$ are asked).
  - Anti-repetition logic prevents the same question from appearing twice in a row.
- ✅ **Victory Flow**: Upon defeating a monster, choose to **Continue Fighting** for more XP or **Exit to Desktop**.
- 🖥️ **Fullscreen, borderless window** that sits on top of everything.
- ⏰ Optional lock:
  - at **Windows login**
  - when the PC **wakes from sleep / hibernate**
  - after a period of **inactivity**
- ⚙️ All behaviour and player stats controlled via a simple `mathlock.settings.json` file.

> ⚠️ **Important:** MathQuizLocker is *not* a security boundary.  
> A knowledgeable user (or an admin account) can still bypass it using Task Manager, Safe Mode, etc.

---

## 🏗️ Requirements

- Windows 10 / 11
- .NET 10.0 Desktop Runtime (Targeting `net10.0-windows`)
- **Assets Folder**: The `Assets/` directory (Dice, KnightSprites, Monsters) must be in the same folder as the executable.

---

## 🚀 Getting Started (Developer)

Clone the repo:

```bash
git clone [https://github.com/DoneFeedingTheFox/MathQuizLocker.git](https://github.com/DoneFeedingTheFox/MathQuizLocker.git)
cd MathQuizLocker
