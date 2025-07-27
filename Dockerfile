FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY ./published-Front/. .
ENTRYPOINT ["dotnet", "MeshtasticMqttExplorer.dll"]