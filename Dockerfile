FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /app

COPY *.fsproj .
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

ENTRYPOINT ["dotnet", "/app/out/AddTriageLabel.dll"]
