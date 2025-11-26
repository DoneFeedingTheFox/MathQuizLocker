# MathQuizLocker 🧮🔒

MathQuizLocker is a small Windows utility that **locks the screen behind a simple math quiz**.

To continue using the PC, the user must correctly solve a number of multiplication questions (the “lille gangetabellen”).  
It is designed as a light-weight “focus / practice” lock – ideal for kids or self-discipline, not as a security product.

---

## ✨ Features

- 🔢 **Multiplication quiz** (configurable range, e.g. 1–10)
- ✅ Require a configurable number of **correct answers in a row**
- 🖥️ **Fullscreen, borderless window** that sits on top of everything
- ⏰ Optional lock:
  - at **Windows login**
  - when the PC **wakes from sleep / hibernate**
  - after a period of **inactivity**
- 📝 Clear inline feedback:
  - Shows whether the answer was correct
  - Shows the correct result on wrong answers
  - Immediately generates a **new question** so you can’t just copy the result
- ⚙️ All behaviour controlled via a simple `mathlock.settings.json` file
- 🧹 No pop-ups, all messages inside a clean centered “card” UI

> ⚠️ **Important:** MathQuizLocker is *not* a security boundary.  
> A knowledgeable user (or an admin account) can still bypass it using Task Manager, Safe Mode, etc.

---

## 🏗️ Requirements

- Windows 10 / 11
- .NET Desktop Runtime compatible with the project’s target (e.g. `.NET x.y-windows`)
- A regular user account (admin recommended only for configuring startup / policies)

---

## 🚀 Getting Started (Developer)

Clone the repo:

```bash
git clone https://github.com/<your-username>/MathQuizLocker.git
cd MathQuizLocker
