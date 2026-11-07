# Working rules

These rules apply to every Claude session in this repo, local or cloud.

## Replies

- Keep every reply short: 4 lines or fewer, with one ask at the end.
- Lead with the result. Use short, plain words.
- Do not use em dashes.
- Recommend one path. Do not list options you will not take.
- Only claim what you checked in this session.

## Safety

- Never print, log or commit a secret value. Keys live in the environment, not in files.
- Never force-push or rewrite history. Use ordinary merge commits.
- Ask before you delete real data.
- Fix bugs by root cause. Reproduce first, then fix.

## Code

- Follow the existing design. Make the minimum change.
- Touch only what the task needs.
