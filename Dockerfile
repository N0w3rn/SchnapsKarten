FROM nginx:alpine
COPY webgl-build/ /usr/share/nginx/html
EXPOSE 80
