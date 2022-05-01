VERSION=0.1.0

rm -Rf publish-*

# OSX
dotnet publish src/RedisQ.Cli -r osx-x64 -o publish-osx -p:PublishTrimmed=true -c Release --self-contained
mv publish-osx/RedisQ.Cli publish-osx/redis-q
zip -r redis-q.$VERSION.osx-x64.zip publish-osx/*

# Linux
dotnet publish src/RedisQ.Cli -r linux-x64 -o publish-linux -p:PublishTrimmed=true -c Release --self-contained
mv publish-linux/RedisQ.Cli publish-linux/redis-q
zip -r redis-q.$VERSION.linux-x64.zip publish-linux/*

# Windows
dotnet publish src/RedisQ.Cli -r win-x64 -o publish-win -p:PublishTrimmed=true -c Release --self-contained
mv publish-win/RedisQ.Cli.exe publish-win/redis-q.exe
zip -r redis-q.$VERSION.win-x64.zip publish-win/*
