FROM mcr.microsoft.com/dotnet/sdk:9.0
WORKDIR /source
COPY Directory.Build.props Directory.Packages.props global.json PANiXiDA.TaskApi.sln ./
COPY src/ src/
COPY tests/ tests/
RUN dotnet restore PANiXiDA.TaskApi.sln
RUN dotnet build PANiXiDA.TaskApi.sln -c Release --no-restore
CMD ["dotnet", "test", "PANiXiDA.TaskApi.sln", "-c", "Release", "--no-build", "--logger", "console;verbosity=normal"]
