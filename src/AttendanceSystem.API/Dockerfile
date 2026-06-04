FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AttendanceSystem.sln ./
COPY src/AttendanceSystem.Domain/ src/AttendanceSystem.Domain/
COPY src/AttendanceSystem.Application/ src/AttendanceSystem.Application/
COPY src/AttendanceSystem.Infrastructure/ src/AttendanceSystem.Infrastructure/
COPY src/AttendanceSystem.API/ src/AttendanceSystem.API/
RUN dotnet restore src/AttendanceSystem.API/AttendanceSystem.API.csproj
RUN dotnet publish src/AttendanceSystem.API/AttendanceSystem.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "AttendanceSystem.API.dll"]
