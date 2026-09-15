import os
import dashscope

# IMPORTANTE:
# Endpoint internacional / Singapore
dashscope.base_http_api_url = "https://dashscope-intl.aliyuncs.com/api/v1"

api_key = os.getenv("DASHSCOPE_API_KEY")

if not api_key:
    raise RuntimeError("DASHSCOPE_API_KEY no está configurada.")

response = dashscope.MultiModalConversation.call(
    api_key=api_key,
    model="qwen3.5-plus",
    messages=[
        {
            "role": "user",
            "content": "Responde únicamente: Qwen conectado correctamente."
        }
    ],
    enable_code_interpreter=True,
    enable_thinking=True,
    result_format="message",
    stream=True
)

for chunk in response:
    print(chunk)