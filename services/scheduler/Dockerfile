FROM alpine:3.22
RUN apk add --no-cache curl tzdata
COPY run-job /usr/local/bin/run-job
COPY crontab /etc/crontabs/root
ENV TZ=UTC
# busybox crond in the foreground, logging to stderr; API_URL and INTERNAL_SECRET come from the environment.
CMD ["sh", "-c", "env | grep -E '^(API_URL|INTERNAL_SECRET)=' > /etc/environment.sh && sed -i 's#^#export #' /etc/environment.sh && sed -i 's#/usr/local/bin/run-job#. /etc/environment.sh; /usr/local/bin/run-job#' /etc/crontabs/root && exec crond -f -l 8 -d 8"]
