# 🚀 Production Deployment Guide

## Multi-Agent Investment Platform - Docker Deployment

This guide covers deploying the Agent Framework application to production using Docker containers on free/low-cost cloud platforms.

---

## 📋 Table of Contents

1. [Prerequisites](#prerequisites)
2. [Quick Start (Local Docker)](#quick-start-local-docker)
3. [Building Docker Images](#building-docker-images)
4. [Free Cloud Deployment Options](#free-cloud-deployment-options)
   - [Railway (Recommended)](#option-1-railway-recommended)
   - [Render](#option-2-render)
   - [Fly.io](#option-3-flyio)
   - [Oracle Cloud Free Tier](#option-4-oracle-cloud-free-tier)
5. [Environment Variables](#environment-variables)
6. [Production Considerations](#production-considerations)
7. [Troubleshooting](#troubleshooting)

---

## Prerequisites

Before deploying, ensure you have:

- ✅ **Docker** installed locally (v20.10+)
- ✅ **Docker Compose** (v2.0+)
- ✅ **Git** installed
- ✅ **OpenAI API Key** (get one at [platform.openai.com](https://platform.openai.com))
- ✅ **GitHub account** (for container registry)

---

## Quick Start (Local Docker)

Test the Docker setup locally before deploying to cloud:

```bash
# 1. Clone the repository
git clone https://github.com/YOUR_USERNAME/microsoft-agent-framework-poc.git
cd microsoft-agent-framework-poc

# 2. Create environment file
cp .env.example .env
# Edit .env and add your OPENAI_API_KEY

# 3. Build and run with Docker Compose
docker-compose up --build

# 4. Access the application
# Frontend: http://localhost:80
# Backend API: http://localhost:5000
# Swagger UI: http://localhost:5000/swagger
```

To stop:
```bash
docker-compose down
```

---

## Building Docker Images

### Build Individual Images

```bash
# Build backend
cd apps/backend
docker build --no-cache -t agent-backend:latest .

# Build frontend
cd ../frontend
docker build --no-cache -t agent-frontend:latest \
  --build-arg VITE_API_URL=https://your-backend-url.com .
```

### Push to Docker Hub

```bash
# 1. Login to Docker Hub
docker login -u alitraboulsi96

# 2. Tag images
docker tag agent-backend:latest alitraboulsi96/agent-backend:latest
docker tag agent-frontend:latest alitraboulsi96/agent-frontend:latest
# 3. Push images
docker push alitraboulsi96/agent-backend:latest
docker push alitraboulsi96/agent-frontend:latest
```

> **Tip:** Create a free Docker Hub account at [hub.docker.com](https://hub.docker.com) if you don't have one.

---

## Free Cloud Deployment Options

### Option 1: Railway (Recommended)

**Railway** offers a generous free tier and easy Docker deployments.

#### Step 1: Create Railway Account
1. Go to [railway.app](https://railway.app)
2. Sign up with GitHub

#### Step 2: Deploy Backend

```bash
# Install Railway CLI
npm install -g @railway/cli

# Login
railway login

# Create new project
railway init

# Deploy backend
cd apps/backend
railway up
```

Or via Dashboard:
1. Click "New Project" → "Deploy from GitHub repo"
2. Select your repository
3. Set root directory to `apps/backend`
4. Add environment variables:
   - `OPENAI_API_KEY`: Your OpenAI key
   - `ASPNETCORE_ENVIRONMENT`: Production
   - `ASPNETCORE_URLS`: http://+:$PORT
5. Deploy

#### Step 3: Deploy Frontend

1. Create new service in same project
2. Set root directory to `apps/frontend`
3. Add build argument:
   - `VITE_API_URL`: Your backend Railway URL
4. Deploy

#### Railway Pricing
- **Free Tier**: $5 credit/month, 500 hours execution
- **Hobby**: $5/month, unlimited hours

---

### Option 2: Render

**Render** offers free web services with automatic HTTPS.

#### Deploy Backend

1. Go to [render.com](https://render.com) → New → Web Service
2. Connect GitHub repository
3. Configure:
   - **Name**: agent-backend
   - **Root Directory**: apps/backend
   - **Runtime**: Docker
   - **Plan**: Free
4. Add environment variables:
   ```
   OPENAI_API_KEY=sk-...
   ASPNETCORE_ENVIRONMENT=Production
   ```
5. Deploy

#### Deploy Frontend

1. New → Static Site (free)
2. Connect same repository
3. Configure:
   - **Root Directory**: apps/frontend
   - **Build Command**: `npm install -g pnpm && pnpm install && pnpm build`
   - **Publish Directory**: `dist`
4. Add environment variable:
   ```
   VITE_API_URL=https://agent-backend.onrender.com
   ```

#### Render Limitations
- Free tier sleeps after 15 minutes of inactivity
- First request after sleep takes ~30 seconds

---

### Option 3: Fly.io

**Fly.io** provides 3 free VMs with good performance.

#### Install Fly CLI

```bash
# Windows (PowerShell)
pwsh -Command "iwr https://fly.io/install.ps1 -useb | iex"

# Mac/Linux
curl -L https://fly.io/install.sh | sh
```

#### Deploy Backend

```bash
cd apps/backend

# Create fly.toml
fly launch --no-deploy

# Set secrets
fly secrets set OPENAI_API_KEY=sk-your-key

# Deploy
fly deploy
```

Create `apps/backend/fly.toml`:
```toml
app = "agent-backend"
primary_region = "iad"

[build]
  dockerfile = "Dockerfile"

[env]
  ASPNETCORE_ENVIRONMENT = "Production"

[http_service]
  internal_port = 5000
  force_https = true
  auto_stop_machines = true
  auto_start_machines = true
  min_machines_running = 0

[[vm]]
  cpu_kind = "shared"
  cpus = 1
  memory_mb = 512
```

#### Deploy Frontend

```bash
cd apps/frontend

fly launch --no-deploy
fly deploy
```

Create `apps/frontend/fly.toml`:
```toml
app = "agent-frontend"
primary_region = "iad"

[build]
  dockerfile = "Dockerfile"
  [build.args]
    VITE_API_URL = "https://agent-backend.fly.dev"

[http_service]
  internal_port = 80
  force_https = true
  auto_stop_machines = true
  auto_start_machines = true
  min_machines_running = 0

[[vm]]
  cpu_kind = "shared"
  cpus = 1
  memory_mb = 256
```

---

### Option 4: Oracle Cloud Free Tier

**Oracle Cloud** offers generous always-free resources (2 AMD VMs or 4 ARM VMs).

#### Step 1: Create Oracle Cloud Account
1. Go to [cloud.oracle.com](https://cloud.oracle.com)
2. Sign up for free tier (credit card required but won't be charged)

#### Step 2: Create Compute Instance

1. Go to Compute → Instances → Create Instance
2. Select:
   - **Shape**: VM.Standard.E2.1.Micro (free)
   - **Image**: Ubuntu 22.04
   - **Boot Volume**: 50GB (free)
3. Download SSH key

#### Step 3: Setup Server

```bash
# SSH into your instance
ssh -i your-key.pem ubuntu@YOUR_INSTANCE_IP

# Install Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker ubuntu

# Install Docker Compose
sudo apt-get install docker-compose-plugin

# Clone repository
git clone https://github.com/YOUR_USERNAME/microsoft-agent-framework-poc.git
cd microsoft-agent-framework-poc

# Create .env file
echo "OPENAI_API_KEY=sk-your-key" > .env

# Run with Docker Compose
docker compose up -d
```

#### Step 4: Configure Firewall

In Oracle Cloud Console:
1. Go to Networking → Virtual Cloud Networks
2. Select your VCN → Security Lists
3. Add Ingress Rules:
   - Port 80 (HTTP)
   - Port 443 (HTTPS)
   - Port 5000 (API - optional)

---

## Environment Variables

### Backend Variables

| Variable | Required | Description | Default |
|----------|----------|-------------|---------|
| `OPENAI_API_KEY` | ✅ Yes | OpenAI API key | - |
| `OPENAI_MODEL` | No | Model to use | gpt-4o-mini |
| `ASPNETCORE_ENVIRONMENT` | No | Environment | Production |
| `ASPNETCORE_URLS` | No | Listen URLs | http://+:5000 |
| `AZURE_OPENAI_ENDPOINT` | No | Azure OpenAI endpoint | - |
| `AZURE_OPENAI_DEPLOYMENT` | No | Azure deployment name | - |

### Frontend Variables

| Variable | Required | Description | Default |
|----------|----------|-------------|---------|
| `VITE_API_URL` | No | Backend API URL | http://localhost:5000 |
| `BACKEND_URL` | No | Backend URL for nginx | http://backend:5000 |

---

## Production Considerations

### 1. Database Persistence

The application uses SQLite by default. For production:

```yaml
# docker-compose.yml
volumes:
  - backend-data:/app/data  # Persists SQLite database
```

For high-availability, consider migrating to:
- **PostgreSQL** (Neon.tech offers free tier)
- **Azure SQL** (with connection string)

### 2. HTTPS/SSL

All recommended platforms (Railway, Render, Fly.io) provide **automatic HTTPS**.

For self-hosted (Oracle Cloud), use **Caddy** as reverse proxy:

```bash
# Install Caddy
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update
sudo apt install caddy

# Create Caddyfile
echo "your-domain.com {
    reverse_proxy localhost:80
}" | sudo tee /etc/caddy/Caddyfile

# Restart Caddy
sudo systemctl restart caddy
```

### 3. Scaling

For higher traffic:
- Use **Railway** or **Fly.io** with multiple instances
- Add **Redis** for session state
- Use **PostgreSQL** instead of SQLite

### 4. Monitoring

Add OpenTelemetry exporter for production monitoring:

```yaml
environment:
  - OTEL_EXPORTER_OTLP_ENDPOINT=https://your-observability-platform
  - OTEL_SERVICE_NAME=agent-backend
```

Free options:
- **Grafana Cloud** (free tier)
- **Honeycomb** (free tier)
- **Axiom** (free tier)

### 5. Secrets Management

Never commit secrets to Git. Use:
- Platform secrets (Railway, Render, Fly.io)
- `.env` files (excluded from Git)
- Cloud secret managers

---

## Troubleshooting

### Common Issues

#### 1. Backend Container Fails to Start

```bash
# Check logs
docker logs agent-backend

# Common fixes:
# - Verify OPENAI_API_KEY is set
# - Check port 5000 is not in use
```

#### 2. Frontend Can't Connect to Backend

```bash
# Check nginx configuration
docker exec agent-frontend cat /etc/nginx/conf.d/default.conf

# Common fixes:
# - Verify BACKEND_URL environment variable
# - Check backend is running and healthy
# - For cross-origin, ensure CORS is configured
```

#### 3. SignalR WebSocket Connection Fails

Ensure your reverse proxy/platform supports WebSockets:
- Railway: ✅ Supported
- Render: ✅ Supported
- Fly.io: ✅ Supported
- Nginx: Add WebSocket headers (already in nginx.conf)

#### 4. Database Issues

```bash
# Check SQLite file permissions
docker exec agent-backend ls -la /app/data/

# Reset database
docker-compose down -v  # WARNING: Deletes data
docker-compose up --build
```

#### 5. Memory Issues on Free Tier

```yaml
# Reduce memory usage in docker-compose.prod.yml
deploy:
  resources:
    limits:
      memory: 512M
```

---

## Quick Reference

### Docker Commands

```bash
# Build
docker-compose build

# Start (detached)
docker-compose up -d

# View logs
docker-compose logs -f

# Stop
docker-compose down

# Rebuild specific service
docker-compose up -d --build backend

# Remove volumes (reset data)
docker-compose down -v
```

### Health Checks

```bash
# Backend health
curl http://localhost:5000/health

# Frontend health
curl http://localhost:80/health

# Backend API
curl http://localhost:5000/api/agents
```

---

## Summary

| Platform | Free Tier | Best For | Limitations |
|----------|-----------|----------|-------------|
| **Railway** | $5/month credit | Easy deployment | Limited hours |
| **Render** | Web services free | Static sites | Cold starts |
| **Fly.io** | 3 free VMs | Performance | CLI required |
| **Oracle Cloud** | 2 free VMs forever | Full control | Setup complexity |

**Recommended Path:**
1. Start with **Railway** or **Render** for quick testing
2. Move to **Fly.io** for better performance
3. Use **Oracle Cloud** for production with full control

---

## Need Help?

- 📚 [Project Documentation](./docs/)
- 🐛 [Open an Issue](https://github.com/YOUR_USERNAME/microsoft-agent-framework-poc/issues)
- 💬 [Discussions](https://github.com/YOUR_USERNAME/microsoft-agent-framework-poc/discussions)
