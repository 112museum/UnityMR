// 集中管理後端網址：NPCRequestManager、StoryPromptManager、GroupChatManager、
// GroupPhotoEmailPrompt 原本各自在 Inspector 裡存一份 backendUrl，散落在好幾個場景檔案裡，
// demo 現場要換網路（例如換熱點）時得记得每個場景、每個元件都改到，漏改一個那個功能就連不上。
// 改成這裡統一管理，之後只要改這一行，全部一起生效，不用再挨個場景挨個元件找。
public static class BackendConfig
{
    private const string Host = "192.168.1.105";

    // NPCRequestManager、StoryPromptManager、GroupChatManager 用的 aibackend（Socket.IO）
    public static string Url = $"http://{Host}:5050";

    // GroupPhotoEmailPrompt 用的 MRmuseum-backend（FastAPI），跟上面共用同一台機器的 IP，
    // 只是 port 不同——換網路時只要改上面的 Host，兩邊會一起跟著換。
    public static string PhotoApiUrl = $"http://{Host}:3000/photo/email";
}
