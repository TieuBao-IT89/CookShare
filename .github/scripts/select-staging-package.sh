#!/usr/bin/env bash
# Pick the newest cookshare-publish artifact that was actually deployed.
# A green workflow run is not enough: deploy.yml can succeed while skipping
# Azure when the staging secrets are absent. Only a develop run of this
# workflow whose deploy step and health check both succeeded is eligible.
set -euo pipefail

if [ -z "${GITHUB_OUTPUT:-}" ]; then
  echo "GITHUB_OUTPUT is required." >&2
  exit 1
fi

artifact_lines=$(gh api --paginate \
  "repos/${GITHUB_REPOSITORY}/actions/artifacts?name=cookshare-publish&per_page=100" \
  --jq ".artifacts[] | select(.expired == false and .workflow_run.id != ${GITHUB_RUN_ID}) | [.created_at, (.workflow_run.id | tostring)] | @tsv")

mapfile -t CANDIDATES < <(
  printf '%s\n' "${artifact_lines}" \
    | sort -r \
    | awk -F '\t' 'NF >= 2 && !seen[$2]++ { print $2 }'
)

RUN_ID=""
for candidate in "${CANDIDATES[@]}"; do
  if [ -z "${candidate}" ]; then
    continue
  fi

  meta=$(gh api "repos/${GITHUB_REPOSITORY}/actions/runs/${candidate}" \
    --jq '[.path, .head_branch, .conclusion] | @tsv')
  IFS=$'\t' read -r path branch conclusion <<< "${meta}"

  if [ "${path}" != ".github/workflows/deploy.yml" ] \
    || [ "${branch}" != "develop" ] \
    || [ "${conclusion}" != "success" ]; then
    continue
  fi

  step_lines=$(gh api --paginate \
    "repos/${GITHUB_REPOSITORY}/actions/runs/${candidate}/jobs?per_page=100" \
    --jq '.jobs[] | select(.name == "Deploy to Azure Staging") | .steps[]? | select(.name == "Deploy to Azure App Service" or .name == "Verify application health") | [.name, .conclusion] | @tsv')

  deploy_ok=false
  health_ok=false
  while IFS=$'\t' read -r step_name step_conclusion; do
    if [ "${step_name}" = "Deploy to Azure App Service" ] && [ "${step_conclusion}" = "success" ]; then
      deploy_ok=true
    fi
    if [ "${step_name}" = "Verify application health" ] && [ "${step_conclusion}" = "success" ]; then
      health_ok=true
    fi
  done <<< "${step_lines}"

  if [ "${deploy_ok}" = "true" ] && [ "${health_ok}" = "true" ]; then
    RUN_ID="${candidate}"
    break
  fi
done

if [ -z "${RUN_ID}" ]; then
  echo "found=false" >> "${GITHUB_OUTPUT}"
  echo "No previous successful deployment package is available."
  exit 1
fi

rm -rf ./previous-package
gh run download "${RUN_ID}" \
  --repo "${GITHUB_REPOSITORY}" \
  --name cookshare-publish \
  --dir ./previous-package
test -s ./previous-package/cookshare.zip
echo "found=true" >> "${GITHUB_OUTPUT}"
echo "run_id=${RUN_ID}" >> "${GITHUB_OUTPUT}"
