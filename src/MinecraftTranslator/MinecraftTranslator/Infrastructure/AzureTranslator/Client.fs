namespace MinecraftTranslator.Infrastructure

// #r "nuget: Azure.AI.Translation.Text"


module AzureTransliterator = 
    open System
    open Azure
    open Azure.Core
    open Azure.AI.Translation.Text
    open MinecraftTranslator.Domain

    type TargetLanguage = string

    let key = Environment.GetEnvironmentVariable("AZURE_TRANSLATOR_KEY")
    let credentials = AzureKeyCredential(key)
    let clientOptions = TextTranslationClientOptions()
    clientOptions.Retry.Delay <- TimeSpan.FromSeconds(5)
    clientOptions.Retry.Mode <- RetryMode.Exponential
    clientOptions.Retry.MaxRetries <- 10
    let client = TextTranslationClient(credentials,"global" , clientOptions)
    
    let mapToOutputLanguage englishText originalText transliteratedText outputLanguage =
        match outputLanguage with
            | Language.zh_ln -> $"{originalText} ({transliteratedText})"
            | Language.zh_py -> transliteratedText
            | Language.py_en -> $"{englishText} ({transliteratedText})"

    let transliterateLanguageFile (chineseFile: LanguageFile) (englishFile: LanguageFile) (outputLanguage: Language)  =
        let language = "zh-Hans"
        let fromScript = "Hans"
        let toScript = "Latn"
        let keys = Map.keys chineseFile
        let originalText =  Map.values chineseFile
        let transliterations = Seq.chunkBySize 10 originalText 
                                |> Seq.map (fun textChunk -> TextTranslationTransliterateOptions(language, fromScript, toScript, textChunk))
                                |> Seq.map (fun textChunk -> client.Transliterate textChunk) 
                                |> Seq.collect (fun (response) -> response.Value) 
        let resultLanguageFile = Seq.zip3 keys originalText transliterations
                                |> Seq.map (fun (key,orig,trans) ->
                                    let englishText = Map.tryFind key englishFile |> Option.defaultValue orig
                                    (key,mapToOutputLanguage englishText orig trans.Text outputLanguage))
                                |> Map.ofSeq
        resultLanguageFile

    