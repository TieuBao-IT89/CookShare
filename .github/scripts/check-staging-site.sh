#!/usr/bin/env bash
# Poll the URL returned by Azure/webapps-deploy. Portal-created apps can use
# <app-name>-<hash>.<region>.azurewebsites.net, so the hostname must not be
# built from AZURE_WEBAPP_NAME.
set -euo pipefail

SITE_URL="${1:-}"
SITE_URL="${SITE_URL%$'\r'}"
SITE_URL="${SITE_URL%/}"

case "${SITE_URL}" in
  https://*|http://*) ;;
  "")
    echo "::error::The deploy action did not return a webapp URL."
    if [ -n "${GITHUB_OUTPUT:-}" ]; then
      echo "health_status=failed" >> "${GITHUB_OUTPUT}"
    fi
    exit 1
    ;;
  *)
    SITE_URL="https://${SITE_URL}"
    ;;
esac

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  echo "site_url=${SITE_URL}" >> "${GITHUB_OUTPUT}"
fi
printf '%s\n' "${SITE_URL}" > "${RUNNER_TEMP:-/tmp}/cookshare-site-url"

echo "Checking ${SITE_URL}/health and ${SITE_URL}/"
for attempt in $(seq 1 18); do
  HEALTH=$(curl --silent --show-error --output /dev/null --write-out "%{http_code}" "${SITE_URL}/health" || echo "000")
  HOME_STATUS=$(curl --silent --show-error --output /dev/null --write-out "%{http_code}" "${SITE_URL}/" || echo "000")

  if [ "${HEALTH}" = "200" ] && [ "${HOME_STATUS}" = "200" ]; then
    if [ -n "${GITHUB_OUTPUT:-}" ]; then
      echo "health_status=200" >> "${GITHUB_OUTPUT}"
    fi
    echo "Application is healthy."
    exit 0
  fi

  echo "Attempt ${attempt}: health HTTP ${HEALTH}, home HTTP ${HOME_STATUS}."
  sleep 10
done

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  echo "health_status=failed" >> "${GITHUB_OUTPUT}"
fi
echo "::error::Health check failed for /health and /."
exit 1
