# LinuxGateway Docker deployment

Docker is the recommended deployment path for LinuxGateway, especially on older hosts such as CentOS 7 where the native `libstdc++` runtime may be too old for the published binary.

## Build from a published LinuxGateway directory

Copy these two files into the published LinuxGateway directory:

```bash
cp deploy/docker/linux-gateway.Dockerfile /path/to/publish/Dockerfile
cp deploy/docker/linux-gateway.dockerignore /path/to/publish/.dockerignore
```

Build and run:

```bash
cd /path/to/publish
docker build -t linux-gateway .
mkdir -p "$HOME/linux-gateway-data"
docker rm -f linux-gateway 2>/dev/null || true
docker run -d --name linux-gateway -p 5090:5090 \
  -e LINUX_GATEWAY_ADMIN_PASSWORD="change-this-password" \
  -e LINUX_GATEWAY_PUBLIC_BASE_URL="http://<your-server-ip>:5090" \
  -e LINUX_GATEWAY_ALLOWED_ORIGINS="http://<your-server-ip>:5090" \
  -v "$HOME/linux-gateway-data:/data" \
  auto-linux-gateway
```

Check status:

```bash
docker ps
docker logs -f linux-gateway
curl -v http://127.0.0.1:5090/api/health
```

Expose TCP `5090` in the cloud security group or host firewall before testing from a browser.
