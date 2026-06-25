FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app
COPY . .

RUN chmod +x /app/LinuxGateway

ENV ASPNETCORE_URLS=http://0.0.0.0:5090
ENV LINUX_GATEWAY_DATA_ROOT=/data

EXPOSE 5090

ENTRYPOINT ["/app/LinuxGateway"]
