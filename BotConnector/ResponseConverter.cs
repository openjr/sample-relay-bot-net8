// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using System.Collections.Generic;
using System.Linq;
using DirectLine = Universal.Microsoft.Bot.Connector.DirectLine;

namespace Microsoft.PowerVirtualAgents.Samples.RelayBotSample
{
    /// <summary>
    /// Class for converting Power Virtual Agents bot replied Direct Line Activity responses to standard Bot Schema activities
    /// You can add customized response converting/parsing logic in this class
    /// </summary>
    public class ResponseConverter
    {
        // Precompiled Regex patterns
        private static readonly System.Text.RegularExpressions.Regex HeaderRegex =
            new System.Text.RegularExpressions.Regex(@"^#{1,6}\s+(.+?)$",
                System.Text.RegularExpressions.RegexOptions.Multiline |
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex CodeBlockRegex =
            new System.Text.RegularExpressions.Regex(@"(```[\s\S]*?```|`[^`]+`)",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex BoldRegex =
            new System.Text.RegularExpressions.Regex(@"(\*\*|__)(?=\S)(.+?)(?<=\S)\1",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex ItalicRegex =
            new System.Text.RegularExpressions.Regex(@"(\*)(?=\S)(.+?)(?<=\S)\1",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex StrikeThroughRegex =
            new System.Text.RegularExpressions.Regex(@"~~(?=\S)(.+?)(?<=\S)~~",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex LinkRegex =
            new System.Text.RegularExpressions.Regex(@"(?<!!)\[([^\]]+)\]\(([^)]+)\)",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        ///  Convert text that comes in Markdown to slack version of markdown.
        /// </summary>
        /// <param name="text">Text to convert from Markdown to slack markdown</param>
        private string ConvertToSlackMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 1. Headers: # Header -> *Header* (Slack doesn't support headers, use Bold)
            text = HeaderRegex.Replace(text, "*$1*");

            // 2. Protect code blocks to ensure their content is not modified
            var codeBlocks = new System.Collections.Generic.List<string>();
            text = CodeBlockRegex.Replace(text, match =>
            {
                codeBlocks.Add(match.Value);
                return $"%CODEBLOCK{codeBlocks.Count - 1}%";
            });

            // 3. Bold: **text** or __text__ -> *text*
            // We use a temporary placeholder (\u0002) to avoid conflict with the subsequent italic conversion
            text = BoldRegex.Replace(text, "\u0002$2\u0002");

            // 4. Italic: *text* -> _text_
            // Standard Markdown _text_ is already compatible with Slack, so we only convert *text*.
            text = ItalicRegex.Replace(text, "_$2_");

            // 5. Restore Bold markers to *
            text = text.Replace("\u0002", "*");

            // 6. Strikethrough: ~~text~~ -> ~text~
            text = StrikeThroughRegex.Replace(text, "~$1~");

            // 7. Links: [text](url) -> <url|text> (Ignoring images starting with !)
            text = LinkRegex.Replace(text, "<$2|$1>");

            // 8. Restore code blocks
            for (var i = 0; i < codeBlocks.Count; i++)
            {
                text = text.Replace($"%CODEBLOCK{i}%", codeBlocks[i]);
            }

            return text;
        }

        /// <summary>
        /// Convert single DirectLine activity into IMessageActivity instance
        /// </summary>
        /// <returns>IMessageActivity object as a message in a conversation</returns>
        /// <param name="directLineActivity">directline activity</param>
        public IMessageActivity ConvertToBotSchemaActivity(DirectLine.Activity directLineActivity)
        {
            if (directLineActivity == null)
            {
                return null;
            }

            var dlAttachments = directLineActivity.Attachments;
            if (dlAttachments != null && dlAttachments.Count() > 0)
            {
                return ConvertToAttachmentActivity(directLineActivity);
            }

            if (directLineActivity.SuggestedActions != null)
            {
                return ConvertToSuggestedActionsAcitivity(directLineActivity);
            }

            if (!string.IsNullOrEmpty(directLineActivity.Text))
            {
                var slackMarkdown = ConvertToSlackMarkdown(directLineActivity.Text);
                return MessageFactory.Text(slackMarkdown);
            }

            return null;
        }

        /// <summary>
        /// Convert a list of DirectLine activities into list of IMessageActivity instances
        /// </summary>
        /// <returns>list of IMessageActivity objects as response messages in a conversation</returns>
        /// <param name="directLineActivities">list of directline activities</param>
        public IList<IMessageActivity> ConvertToBotSchemaActivities(List<DirectLine.Activity> directLineActivities)
        {
            return (directLineActivities == null || directLineActivities.Count() == 0)
                ? new List<IMessageActivity>()
                : directLineActivities
                    .Select(directLineActivity => ConvertToBotSchemaActivity(directLineActivity))
                    .ToList();
        }

        private IMessageActivity ConvertToAttachmentActivity(DirectLine.Activity directLineActivity)
        {
            var botSchemaAttachments = directLineActivity.Attachments.Select(directLineAttachment => new Attachment()
            {
                ContentType = directLineAttachment.ContentType,
                ContentUrl = directLineAttachment.ContentUrl,
                Content = directLineAttachment.Content,
                Name = directLineAttachment.Name,
                ThumbnailUrl = directLineAttachment.ThumbnailUrl,
            }).ToList();

            return MessageFactory.Attachment(
                botSchemaAttachments,
                text: directLineActivity.Text,
                ssml: directLineActivity.Speak,
                inputHint: directLineActivity.InputHint);
        }

        private IMessageActivity ConvertToSuggestedActionsAcitivity(DirectLine.Activity directLineActivity)
        {
            var directLineSuggestedActions = directLineActivity.SuggestedActions;
            return MessageFactory.SuggestedActions(
                actions: directLineSuggestedActions.Actions?.Select(action => action.Title).ToList(),
                text: directLineActivity.Text,
                ssml: directLineActivity.Speak,
                inputHint: directLineActivity.InputHint);
        }
    }
}