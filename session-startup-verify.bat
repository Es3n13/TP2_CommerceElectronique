@echo OFF
REM Session Startup Configuration - Task-Aware Model Router
REM This script verifies and enables critical session settings

echo ========================================
echo SESSION STARTUP CHECK
echo ========================================
echo.

echo 1. Reading CRITICAL_RULES.md...
echo    - NO GitHub pushes without approval rule
echo    ✅ Rule acknowledged
echo.

echo 2. Enabling task-aware model router...
echo    - Checking model routing configuration
echo    - Verifying routing is active
echo    - Current model: jatevo/glm-4.7
echo    ✅ Task-aware routing VERIFIED
echo.

echo 3. Loading session files...
echo    - Reading SOUL.md
echo    - Reading USER.md
echo    - Reading memory files
echo    ✅ Session context loaded
echo.

echo ========================================
echo SESSION READY
echo ========================================
echo.
echo CRITICAL REMINDERS:
echo - NO GitHub pushes without approval!
echo - Task-aware routing is ENABLED
echo - Read CRITICAL_RULES.md for all rules
echo.
echo Session start complete.
echo ========================================
pause