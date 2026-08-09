# Deploy To Nginx Server

This project runs the API in Docker and lets Nginx proxy traffic to the container on the same server.

## 1. Build the image locally

```bash
docker build -t threads-api:local .
docker run --rm -p 8080:8080 --env-file deploy/.env.production.example threads-api:local
```

The container listens on port `8080`.

## 2. Registry placeholders to replace

Replace these placeholders in your GitHub repository settings:

- `vars.CONTAINER_REGISTRY`: `ghcr.io`, `docker.io`, or your private registry host
- `vars.CONTAINER_IMAGE_REPOSITORY`: for example `your-org/threads-api`
- `vars.DEPLOY_HOST`: your Linux server IP or hostname
- `vars.DEPLOY_PORT`: SSH port, usually `22`
- `vars.DEPLOY_USER`: SSH user on the server
- `vars.DEPLOY_PATH`: deployment directory on the server, for example `/opt/threads-api`
- `vars.NGINX_SITE_PATH`: destination config path, for example `/etc/nginx/conf.d/threads.conf`

Add these GitHub secrets:

- `REGISTRY_USERNAME`
- `REGISTRY_PASSWORD`
- `DEPLOY_SSH_KEY`
- `APP_ENV_FILE`: full production `.env` file content based on `deploy/.env.production.example`

## 3. Server files used during deployment

- `docker-compose.yml`: runs the image and binds `127.0.0.1:7000` to container port `8080`
- `deploy/nginx/threads.conf`: proxies public traffic to `127.0.0.1:7000`
- `deploy/.env.production.example`: example runtime variables

## 4. First-time server setup

Install Docker, Docker Compose plugin, and Nginx on the server, then create the deploy directory:

```bash
sudo mkdir -p /opt/threads-api
sudo chown -R <DEPLOY_USER>:<DEPLOY_USER> /opt/threads-api
```

Copy the real `.env` content from `deploy/.env.production.example` and replace every placeholder value.

## 5. Manual deploy flow on the server

If you want to deploy manually before using GitHub Actions:

```bash
docker login <REGISTRY_HOST> -u <REGISTRY_USERNAME>
cd /opt/threads-api
export IMAGE_URI=<REGISTRY_HOST>/<IMAGE_REPOSITORY>
export IMAGE_TAG=latest
docker compose pull
docker compose up -d
sudo cp deploy/nginx/threads.conf /etc/nginx/conf.d/threads.conf
sudo nginx -t
sudo systemctl reload nginx
```

## 6. How the GitHub workflow deploys

The workflow in `.github/workflows/build-deploy.yaml`:

1. Builds the Docker image from `Dockerfile`
2. Pushes tags for `latest` and the commit SHA
3. Copies `docker-compose.yml` and `deploy/nginx/threads.conf` to the server
4. Writes the production `.env` file from `secrets.APP_ENV_FILE`
5. Pulls the new image and restarts the container
6. Replaces the Nginx site config and reloads Nginx
