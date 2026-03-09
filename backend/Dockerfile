FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY GymApp.sln .
COPY GymApp.Domain/GymApp.Domain.csproj GymApp.Domain/
COPY GymApp.Infra/GymApp.Infra.csproj GymApp.Infra/
COPY GymApp.Api/GymApp.Api.csproj GymApp.Api/
RUN dotnet restore

COPY . .
RUN dotnet publish GymApp.Api/GymApp.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000
ENTRYPOINT ["dotnet", "GymApp.Api.dll"]
