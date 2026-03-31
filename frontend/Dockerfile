FROM nginx:alpine
COPY . /usr/share/nginx/html
# Stamp all HTML files with the build timestamp (YYYYMMDDHHmm)
# Replaces any existing ?v=... so every deploy invalidates the CSS cache automatically
RUN find /usr/share/nginx/html -name "*.html" -exec \
    sed -i "s/\?v=[0-9]\{8,12\}/?v=$(date +%Y%m%d%H%M)/g" {} \;
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
