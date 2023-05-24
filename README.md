
# 📌 **Open AI QA機器人和ChatGPT API實作**

![image](https://github.com/MomoChenisMe/OpenAIAPI/blob/main/DemoImage/OpenAIAPI-1.png)
![image](https://github.com/MomoChenisMe/OpenAIAPI/blob/main/DemoImage/OpenAIAPI-2.png)

使用Open AI API建立的ChatGPT和QA機器人<br>
1. Google OpenID Connect登入服務<br>
2. ChatGPT3.5/4聊天室操作API<br>
3. QA服務<br>
4. Chat GPT 3.5/4串接<br>
5. Embedding文本轉換和寫入<br><br>

## 📒 **環境變數相關資訊**

### SeqLog設定
- **serverUrl** : Seqlog Server Url
- **apiKey** : Seqlog API Key

### MSSQL設定
- **ConnectionStrings__DefaultConnection** : SQL連線字串
- **Connection__Key** : SQL連線密碼

### PathBase設定
- **HostUrl__PathBase** : 代理路徑設定

### OpenAI API設定
- **OpenAI__Key** : Open AI API Key
- **OpenAI__ChatGPTModel** : ChatGPT的Model(預設:gpt-3.5-turbo)
- **OpenAI__EmbeddingModel** : Embedding的Model(預設:text-embedding-ada-002)
- **OpenAI__ChatGPTCompletionMAXTokenSize** : ChatGPT回答時的Token最大數量(預設:2048)
- **OpenAI__QAGPTCompletionMAXTokenSize** : QA GPT回答時的Token最大數量(預設:1024)
- **OpenAI__TotalTokenSize** : ChatGPT的提問和回應Token數量限制(預設:4096)(gpt-3.5-turbo最大:4096)
<br>

## 📒 **DB相關資訊**

**資料庫** : OpenAI



