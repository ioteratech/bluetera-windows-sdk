echo off

where protoc
if %ERRORLEVEL% NEQ 0 echo protocol buffers compiler was not found

for %%p in (HelloBlueteraWinRt HelloBlueteraWpf) DO (
    protoc -I=../protobuf --csharp_out=%%p ../protobuf/bluetera_messages.proto
)