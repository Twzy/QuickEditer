window.quickEditer = window.quickEditer || {};

window.quickEditer.registerArduinoCompletion = function () {
    if (!window.monaco) return;
    if (window.quickEditer._arduinoCompletionRegistered) return;
    window.quickEditer._arduinoCompletionRegistered = true;

    const monaco = window.monaco;

    const keywords = [
        { label: 'pinMode', kind: monaco.languages.CompletionItemKind.Function, insertText: 'pinMode(${1:pin}, ${2:OUTPUT})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: '设置引脚模式' },
        { label: 'digitalWrite', kind: monaco.languages.CompletionItemKind.Function, insertText: 'digitalWrite(${1:pin}, ${2:HIGH})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: '数字输出' },
        { label: 'digitalRead', kind: monaco.languages.CompletionItemKind.Function, insertText: 'digitalRead(${1:pin})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: '数字输入' },
        { label: 'analogRead', kind: monaco.languages.CompletionItemKind.Function, insertText: 'analogRead(${1:pin})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: '模拟输入' },
        { label: 'analogWrite', kind: monaco.languages.CompletionItemKind.Function, insertText: 'analogWrite(${1:pin}, ${2:value})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: 'PWM 输出' },
        { label: 'delay', kind: monaco.languages.CompletionItemKind.Function, insertText: 'delay(${1:ms})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: '延时毫秒' },
        { label: 'delayMicroseconds', kind: monaco.languages.CompletionItemKind.Function, insertText: 'delayMicroseconds(${1:us})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: '延时微秒' },
        { label: 'millis', kind: monaco.languages.CompletionItemKind.Function, insertText: 'millis()', documentation: '开机毫秒数' },
        { label: 'micros', kind: monaco.languages.CompletionItemKind.Function, insertText: 'micros()', documentation: '开机微秒数' },
        { label: 'map', kind: monaco.languages.CompletionItemKind.Function, insertText: 'map(${1:value}, ${2:fromLow}, ${3:fromHigh}, ${4:toLow}, ${5:toHigh})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: '数值映射' },
        { label: 'constrain', kind: monaco.languages.CompletionItemKind.Function, insertText: 'constrain(${1:value}, ${2:min}, ${3:max})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: '限制范围' },
        { label: 'Serial.begin', kind: monaco.languages.CompletionItemKind.Function, insertText: 'Serial.begin(${1:115200})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: '初始化串口' },
        { label: 'Serial.print', kind: monaco.languages.CompletionItemKind.Function, insertText: 'Serial.print(${1:value})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: '串口打印' },
        { label: 'Serial.println', kind: monaco.languages.CompletionItemKind.Function, insertText: 'Serial.println(${1:value})', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: '串口打印并换行' },
        { label: 'Serial.available', kind: monaco.languages.CompletionItemKind.Function, insertText: 'Serial.available()', documentation: '串口可读字节数' },
        { label: 'Serial.read', kind: monaco.languages.CompletionItemKind.Function, insertText: 'Serial.read()', documentation: '读取一个字节' },
        { label: 'setup', kind: monaco.languages.CompletionItemKind.Snippet, insertText: 'void setup() {\n\t${1}\n}', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: '初始化函数' },
        { label: 'loop', kind: monaco.languages.CompletionItemKind.Snippet, insertText: 'void loop() {\n\t${1}\n}', insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet, documentation: '主循环' },
        { label: 'HIGH', kind: monaco.languages.CompletionItemKind.Constant, insertText: 'HIGH' },
        { label: 'LOW', kind: monaco.languages.CompletionItemKind.Constant, insertText: 'LOW' },
        { label: 'INPUT', kind: monaco.languages.CompletionItemKind.Constant, insertText: 'INPUT' },
        { label: 'OUTPUT', kind: monaco.languages.CompletionItemKind.Constant, insertText: 'OUTPUT' },
        { label: 'INPUT_PULLUP', kind: monaco.languages.CompletionItemKind.Constant, insertText: 'INPUT_PULLUP' },
        { label: 'LED_BUILTIN', kind: monaco.languages.CompletionItemKind.Constant, insertText: 'LED_BUILTIN' }
    ];

    ['cpp', 'c', 'plaintext'].forEach(lang => {
        monaco.languages.registerCompletionItemProvider(lang, {
            provideCompletionItems: function (model, position) {
                if (!model || !position) return { suggestions: [] };

                const word = model.getWordUntilPosition(position);
                const range = {
                    startLineNumber: position.lineNumber,
                    endLineNumber: position.lineNumber,
                    startColumn: word.startColumn,
                    endColumn: word.endColumn
                };
                return {
                    suggestions: keywords.map(k => ({ ...k, range }))
                };
            }
        });
    });
};

window.quickEditer.setMonacoTheme = function (theme) {
    if (!window.monaco) return;
    window.monaco.editor.setTheme(theme);
};