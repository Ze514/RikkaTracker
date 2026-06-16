## Git 提交消息生成规范
生成提交消息时必须严格遵循以下格式规则：
1.  第一行为单行摘要，字符数不超过 50，使用祈使句语气
2.  第二行必须为空行
3.  从第三行开始编写完整的改动详情，说明改动原因与影响
4.  所有提交消息统一使用中文编写
5.  优先遵循 Conventional Commits 格式：`类型(模块): 摘要`
    - 类型可选：feat / fix / docs / style / refactor / perf / test / build / ci / chore