@echo off
REM Create directories if they don't exist
mkdir "C:\redis-data" 2>$null
mkdir "C:\redis-data\pid" 2>$null

REM Stop and remove existing container if it exists
powershell -Command "docker rm -f altepost-persistent-res 2>$null"

REM Create Redis configuration file with PID file disabled
echo dir /data > "C:\redis-data\redis.conf"
echo dbfilename dump.rdb >> "C:\redis-data\redis.conf"
echo pidfile "" >> "C:\redis-data\redis.conf"
echo appendonly no >> "C:\redis-data\redis.conf"

REM Save directives - customize these as needed
echo save 30 1 >> "C:\redis-data\redis.conf"

REM Run Redis container with root user
powershell -Command "docker run -d --name altepost-persistent-res --user root -p 6379:6379 -v C:\redis-data:/data --restart unless-stopped redis:latest redis-server /data/redis.conf"

echo Redis container started. Checking logs...
timeout /t 2 > nul
powershell -Command "docker logs altepost-persistent-res"
echo.
echo To verify data loading, run: docker exec -it altepost-persistent-res redis-cli
pause