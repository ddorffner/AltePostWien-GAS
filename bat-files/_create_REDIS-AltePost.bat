@echo off
powershell -Command "docker run -p 6379:6379 --name altepost -d redis redis-server --save 60 1 --loglevel warning"
pause