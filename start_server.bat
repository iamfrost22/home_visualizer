@echo off
cd /d "%~dp0"
echo Starting API Server on 0.0.0.0:8000 for local network access...

IF EXIST venv\Scripts\activate.bat (
    call venv\Scripts\activate.bat
) ELSE IF EXIST .venv\Scripts\activate.bat (
    call .venv\Scripts\activate.bat
) ELSE (
    echo WARNING: Could not find virtual environment. Ensure dependencies are installed!
)

uvicorn server.main:app --reload --host 0.0.0.0 --port 8000
pause
