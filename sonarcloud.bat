dotnet sonarscanner begin /k:"yellowfeather_DbfDataReader" /o:"yellowfeather" /d:sonar.host.url="https://sonarcloud.io" /d:sonar.login="da2e5efd607eaa36e75b97d88c4ba1344e589304"
dotnet build DbfDataReader.sln
dotnet sonarscanner end /d:sonar.login="da2e5efd607eaa36e75b97d88c4ba1344e589304"
