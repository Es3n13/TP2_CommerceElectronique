# SESSION_STARTUP_CONFIG.md - System Startup Requirements

**Purpose:** Critical system settings that MUST be configured at session startup every time.

---

## 🧠 Model Routing Configuration

**CRITICAL: Task-aware model router MUST be enabled at system startup.**

### Configuration Requirements

The task-aware model router should be configured to:
- Route different task types to appropriate models
- Enable automatic model selection based on task complexity
- Provide fallback mechanisms when primary models are unavailable

### Startup Checklist

When starting a session, ensure:
1. ✅ Task-aware model routing is ENABLED
2. ✅ Default model is configured (jatevo/glm-4.7 or better)
3. ✅ Model aliases are defined for task-specific routing
4. ✅ Routing is active (not just configured)

### Verification

To verify routing is enabled:
- Check session status: Should show routing configuration active
- Look for model switching behavior in complex tasks
- Verify different models are used for different task types

### Configuration Location

This configuration is typically set in:
- OpenClaw configuration files (models.json or similar)
- Environment variables for model routing
- System startup scripts

---

## ⚠️ This Configuration Is CRITICAL

Without task-aware routing enabled:
- Single fixed model for all tasks
- Suboptimal performance on varied tasks
- Inability to handle complex multi-step tasks
- Reduced overall capability

**ALWAYS verify this is enabled at session startup!**

---

**Last Updated:** April 4, 2026
**Priority:** CRITICAL - First thing to verify in every session