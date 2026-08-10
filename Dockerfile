FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["Threads.Api/Threads.Api.csproj", "Threads.Api/"]
COPY ["Threads.Application/Threads.Application.csproj", "Threads.Application/"]
COPY ["Threads.Domain/Threads.Domain.csproj", "Threads.Domain/"]
COPY ["Threads.Infrastracture/Threads.Infrastracture.csproj", "Threads.Infrastracture/"]

RUN dotnet restore "Threads.Api/Threads.Api.csproj"

COPY . .

WORKDIR "/src/Threads.Api"

RUN dotnet publish "Threads.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Threads.Api.dll"]
