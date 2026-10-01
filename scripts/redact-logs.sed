# Redacts container logs before deploy-prod prints them to the job log or
# uploads them as an artifact (#432). This repository is PUBLIC: job logs and
# artifacts are readable by anyone signed in to GitHub. Prompts reach logs as
# query strings (`/simple?q=<prompt>`) in request lines and referrers, so:
#
# 1. Drop the query string from any path-like token (request lines, URLs,
#    nginx error-log `request: "GET /simple?q=… HTTP/1.1"`).
# 2. Drop nginx error-log `, referrer: "…"` fields entirely.
#
# Usage: … | sed -E -f scripts/redact-logs.sed
s#(/[^ "?]*)[?][^ "]*#\1#g
s/, referrer: "[^"]*"//g
