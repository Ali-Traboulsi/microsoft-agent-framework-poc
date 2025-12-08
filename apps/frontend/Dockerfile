# ==========================================
# Frontend Dockerfile - React + Vite
# Multi-stage build with nginx for production
# ==========================================

# Stage 1: Build
FROM node:20-alpine AS build
WORKDIR /app

# Install pnpm
RUN npm install -g pnpm

# Copy package files
COPY package.json pnpm-lock.yaml ./

# Install dependencies
RUN pnpm install --frozen-lockfile

# Copy source files
COPY . .

# Build argument for API URL (set at build time)
ARG VITE_API_URL=https://agent-backend-api-dev.up.railway.app
ENV VITE_API_URL=$VITE_API_URL

# Build the app
RUN pnpm build

# Stage 2: Production with nginx
FROM nginx:alpine AS production
WORKDIR /usr/share/nginx/html

# Remove default nginx static assets
RUN rm -rf ./*

# Copy built assets from build stage
COPY --from=build /app/dist .

# Copy custom nginx configuration as template
COPY nginx.conf /etc/nginx/templates/default.conf.template

# Create non-root user and set permissions
RUN adduser -D -H -u 1001 -s /sbin/nologin nginx-user && \
    chown -R nginx-user:nginx-user /usr/share/nginx/html && \
    chown -R nginx-user:nginx-user /var/cache/nginx && \
    chown -R nginx-user:nginx-user /var/log/nginx && \
    touch /var/run/nginx.pid && \
    chown nginx-user:nginx-user /var/run/nginx.pid && \
    mkdir -p /etc/nginx/conf.d && \
    chown -R nginx-user:nginx-user /etc/nginx/conf.d

# Default backend URL (override with environment variable)
ENV BACKEND_URL=https://agent-backend-api-dev.up.railway.app
# Backend host (just the hostname without protocol) for Host header
ENV BACKEND_HOST=agent-backend-api-dev.up.railway.app

# Expose protocol
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:80/ || exit 1

# Use envsubst to replace variables in nginx config at runtime, then start nginx
CMD ["sh", "-c", "envsubst '${BACKEND_URL} ${BACKEND_HOST}' < /etc/nginx/templates/default.conf.template > /etc/nginx/conf.d/default.conf && nginx -g 'daemon off;'"]
