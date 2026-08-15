// 集中管理後端網址：NPCRequestManager、StoryPromptManager、GroupChatManager、
// GroupPhotoEmailPrompt 原本各自在 Inspector 裡存一份 backendUrl，散落在好幾個場景檔案裡，
// demo 現場要換網路（例如換熱點）時得记得每個場景、每個元件都改到，漏改一個那個功能就連不上。
// 改成這裡統一管理，之後只要改這一行，全部一起生效，不用再挨個場景挨個元件找。
public static class BackendConfig
{
    public static string Url = "http://172.20.10.8:5050";
}
