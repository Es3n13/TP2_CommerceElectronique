# TOOLS.md - Local Notes

Skills define _how_ tools work. This file is for _your_ specifics — the stuff that's unique to your setup.

## What Goes Here

Things like:

- Camera names and locations
- SSH hosts and aliases
- Preferred voices for TTS
- Speaker/room names
- Device nicknames
- Anything environment-specific

## Git Workflow Rules

### ⛔ NO GITHUB PUSHES WITHOUT APPROVAL

**CRITICAL RULE:** Before ANY git push operation, ALWAYS ask for explicit approval from the user.

**Workflow:**
```bash
# 1. Make changes
git add -A

# 2. Commit changes
git commit -m "message"

# 3. STOP HERE - Ask for approval before pushing
# Show commit summary:
#   - Branch name
#   - Commit hash
#   - Files modified
#   - Summary of changes

# 4. Ask: "Ready to push to GitHub? Confirm: y/n"

# 5. ONLY if user says "yes"/"y"/"approved"/"push":
git push origin V.Alpha
```

**Approval Message Template:**
```
✅ Changes committed. Ready to push to GitHub?

📋 Details:
  Branch: V.Alpha
  Commit: abc1234
  Files: file1.cs, file2.json
  Summary: [brief description]

Confirm push to GitHub? y/n
```

**ONLY push after user says:**
- "yes"
- "y"
- "approved"
- "push"

**No exceptions! This applies to EVERY push.** ⛔

---

## Examples

### Cameras

- living-room → Main area, 180° wide angle
- front-door → Entrance, motion-triggered

### SSH

- home-server → 192.168.1.100, user: admin

### TTS

- Preferred voice: "Nova" (warm, slightly British)
- Default speaker: Kitchen HomePod

---

## Grocery Price Tracking System

**Location:** Rimouski, QC
**Stores:** Walmart, Super C, Costco, IGA, Maxi

**How to use:**
- "add milk, eggs to my grocery list" → Add items
- "check cheapest prices for my grocery list" → Search prices
- "show my grocery list" → View current list
- "remove eggs from my grocery list" → Remove item
- "clear my grocery list" → Reset

**Files:**
- `grocery-list.json` - Current grocery list
- `scripts/grocery-price-search.py` - Price search tool
- `GROCERIES.md` - Documentation

---

*Remember: Always ask before pushing to GitHub!*