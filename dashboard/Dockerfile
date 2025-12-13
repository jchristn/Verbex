FROM node:22-alpine AS build
WORKDIR /app

# Copy package files and install dependencies
COPY package.json package-lock.json ./
RUN npm ci

# Copy source and build
COPY . .
RUN npm run build

# Production image with nginx
FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html

# Install diagnostic tools
RUN apk add --no-cache iputils bind-tools iproute2 curl wget vim file busybox-extras net-tools

# Copy nginx config for SPA routing
RUN echo 'server { \
    listen 8200; \
    location / { \
        root /usr/share/nginx/html; \
        index index.html; \
        try_files $uri $uri/ /index.html; \
    } \
}' > /etc/nginx/conf.d/default.conf

EXPOSE 8200
CMD ["nginx", "-g", "daemon off;"]
