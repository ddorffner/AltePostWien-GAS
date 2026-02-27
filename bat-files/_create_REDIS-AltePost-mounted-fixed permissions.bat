@echo off
REM Remove existing container if it exists
powershell -Command "docker rm -f altepost-mounted 2>$null"

REM Create required directories if they don't exist
mkdir "C:\localredis\data" 2>$null
mkdir "C:\localredis\conf" 2>$null

REM Create Redis configuration file if it doesn't exist
if not exist "C:\localredis\conf\redis.conf" (
    echo dir /data/data > "C:\localredis\conf\redis.conf"
    echo appendonly yes >> "C:\localredis\conf\redis.conf"
)

REM Run Redis container using default redis user permissions
powershell -Command "docker run -d --restart unless-stopped -p 6379:6379 -v C:\localredis\data:/data/data -v C:\localredis\conf:/data/conf --name altepost-mounted redis:latest redis-server /data/conf/redis.conf"

echo Redis container started. Check status with: docker ps
pause