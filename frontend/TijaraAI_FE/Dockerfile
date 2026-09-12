# Stage 1: Build the Angular application
FROM node:20-alpine AS build
WORKDIR /app

# Install dependencies
COPY package*.json ./
RUN npm ci

# Copy application code and build for production
COPY . .
RUN npm run build -- --configuration production

# Stage 2: Serve application with Nginx
FROM nginx:alpine

# Remove default nginx website
RUN rm -rf /usr/share/nginx/html/*

# Copy build artifacts from stage 1
# Note: Angular 17+ application builder outputs to dist/<app-name>/browser
COPY --from=build /app/dist/tijara-ai-fe/browser /usr/share/nginx/html

# Copy custom Nginx configuration for Angular SPA routing
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
