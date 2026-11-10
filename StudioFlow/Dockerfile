FROM node:24-alpine AS frontend
WORKDIR /frontend
COPY package*.json tsconfig.json ./
RUN npm ci
COPY ClientApp ./ClientApp
RUN npm run build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY StudioFlow.csproj .
RUN dotnet restore
COPY . .
COPY --from=frontend /frontend/wwwroot/js ./wwwroot/js
RUN dotnet publish -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "StudioFlow.dll"]
