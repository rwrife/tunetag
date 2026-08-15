# Local AI payload contract

TuneTag's optional AI assist sends requests only when the user enables AI in the app.
Default mode is off and performs no AI network calls.

## Endpoint

- Base URL: user-configured (default `http://127.0.0.1:11434/v1`)
- Request path: `POST /chat/completions`
- Probe path: `GET /models`

## Request body (chat completions)

```json
{
  "model": "llama3.2",
  "temperature": 0.1,
  "response_format": { "type": "json_object" },
  "messages": [
    { "role": "system", "content": "..." },
    {
      "role": "user",
      "content": "{\"context_fields\":[\"artist\",\"album\"],\"target_fields\":[\"title\",\"genre\"],\"tracks\":[{\"id\":\"track-1\",\"context\":{\"artist\":\"Miles Davis\",\"album\":\"Kind of Blue\"},\"missing_targets\":[\"title\",\"genre\"]}]}"
    }
  ]
}
```

### Data-minimization rules

- Only user-selected **context fields** are sent (`artist`, `album`, `albumArtist`).
- Only tracks with selected **missing target fields** are included.
- TuneTag expects suggestions for `title`, `album`, and/or `genre` only.
- Suggestions are proposals until the user clicks **Apply AI Suggestions**.
