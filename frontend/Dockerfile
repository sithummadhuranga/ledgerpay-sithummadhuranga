# Build the app, then serve the files with nginx. nginx also passes /api to the API, so the browser sees one origin.
FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# The unprivileged nginx image runs as a normal user and listens on 8080.
FROM nginxinc/nginx-unprivileged:stable-alpine AS web
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
