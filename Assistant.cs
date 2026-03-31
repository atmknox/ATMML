using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using static ATMML.AssistantService;

namespace ATMML
{
	public class AssistantService
	{
		static HttpClient _client;
		static string _threadId;

		public delegate void AssistantEventHandler(object sender, AssistantEventArgs e);
		public static event AssistantEventHandler Event;

		protected static void OnAssistantEvent(AssistantEventArgs e)
		{
			Event?.Invoke(null, e);
		}

		static public async void Request(string input)
		{
			try
			{
				if (_client == null)
				{
					_client = new HttpClient();

					var apiKey = "YOUR_OPENAI_API_KEY_HERE";
					_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
					_client.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");

					//var endpoint0 = "https://api.openai.com/v1/threads";
					//var content0 = new StringContent("", Encoding.UTF8, "application/json");
					//var response0 = await _client.PostAsync(endpoint0, content0);
					//var responseString0 = await response0.Content.ReadAsStringAsync();
					//AssistantThread data0 = JsonConvert.DeserializeObject<AssistantThread>(responseString0);
					//_threadId = data0.id;
				}

				_threadId = "thread_3kzrcQ0pxyd34gxM2YJBWauU";

				var endpoint1 = "https://api.openai.com/v1/threads/" + _threadId + "/messages";
				var request = new AssistantRequest()
				{
					role = "user",
					content = input
				};
				var json1 = JsonConvert.SerializeObject(request, Formatting.Indented);
				var content1 = new StringContent(json1, Encoding.UTF8, "application/json");
				var response1 = await _client.PostAsync(endpoint1, content1);
				var responseString1 = await response1.Content.ReadAsStringAsync();

				var endpoint2 = "https://api.openai.com/v1/threads/" + _threadId + "/runs";
				string json2 = @"{ 
				                ""assistant_id"": ""asst_IW0cMbow5eJwPxf9YDnoxoDK"",
				                ""instructions"": ""respond with the just the share sizes""
				}";
				var content2 = new StringContent(json2, Encoding.UTF8, "application/json");
				var response2 = await _client.PostAsync(endpoint2, content2);
				var responseString2 = await response2.Content.ReadAsStringAsync();

				ChatRun data2 = JsonConvert.DeserializeObject<ChatRun>(responseString2);
				var runId = data2.id;
				var status = data2.status;

				var period = new TimeSpan(0, 0, 0, 0, 500);// try every half second for 10 seconds
				var time = DateTime.Now;
				var count = 1;
				while (count < 20 && status != "completed")
				{
					var now = DateTime.Now;
					if (now >= time + period)
					{
						time = now;
						var endpoint3 = "https://api.openai.com/v1/threads/" + _threadId + "/runs/" + runId;
						var response3 = await _client.GetAsync(endpoint3);
						var responseString3 = await response3.Content.ReadAsStringAsync();
						ChatRun data3 = JsonConvert.DeserializeObject<ChatRun>(responseString3);
						status = data3.status;
						count++;
					}
				}

				var endpoint4 = "https://api.openai.com/v1/threads/" + _threadId + "/messages";
				var response4 = await _client.GetAsync(endpoint4);
				var responseString4 = await response4.Content.ReadAsStringAsync();

				ChatMessage data4 = JsonConvert.DeserializeObject<ChatMessage>(responseString4);

				var x = data4.data.SelectMany(cm => cm.content.Select(c => c.text.value)).ToList();
				var message = string.Join("\n", x);
				if (message.Length == 0) message = "No suggestion.";

				OnAssistantEvent(new AssistantEventArgs(AssistantEventType.Message, message));
			}
			catch (Exception)
			{
				_client = null;
			}
		}

		public class AssistantRequest
		{
			public string role { get; set; }
			public string content { get; set; }
		}

		public class Text
		{
			public string value { get; set; }
			public List<object> annotations { get; set; }
		}

		public class Content
		{
			public string type { get; set; }
			public Text text { get; set; }
		}
		public class Metadata
		{
		}

		public class ChatMessageData
		{
			public string id { get; set; }
			public string @object { get; set; }
			public int created_at { get; set; }
			public object assistant_id { get; set; }
			public string thread_id { get; set; }
			public object run_id { get; set; }
			public string role { get; set; }
			public List<Content> content { get; set; }
			public List<object> attachments { get; set; }
			public Metadata metadata { get; set; }
		}

		public class ChatMessage
		{
			public string @object { get; set; }
			public List<ChatMessageData> data { get; set; }
			public string first_id { get; set; }
			public string last_id { get; set; }
			public bool has_more { get; set; }
		}

		public class FileSearch
		{
			public RankingOptions ranking_options { get; set; }
		}

		public class RankingOptions
		{
			public string ranker { get; set; }
			public double score_threshold { get; set; }
		}

		public class Tool
		{
			public string type { get; set; }
			public FileSearch file_search { get; set; }
		}

		public class ToolResources
		{
		}

		public class TruncationStrategy
		{
			public string type { get; set; }
			public object last_messages { get; set; }
		}
		public class Usage
		{
			public int prompt_tokens { get; set; }
			public int completion_tokens { get; set; }
			public int total_tokens { get; set; }
		}

		public class ChatRun
		{
			public string id { get; set; }
			public string @object { get; set; }
			public int created_at { get; set; }
			public string assistant_id { get; set; }
			public string thread_id { get; set; }
			public string status { get; set; }
			public object started_at { get; set; }
			public object expires_at { get; set; }
			public object cancelled_at { get; set; }
			public object failed_at { get; set; }
			public object completed_at { get; set; }
			public object required_action { get; set; }
			public object last_error { get; set; }
			public string model { get; set; }
			public string instructions { get; set; }
			public List<Tool> tools { get; set; }
			public ToolResources tool_resources { get; set; }
			public Metadata metadata { get; set; }
			public double temperature { get; set; }
			public double top_p { get; set; }
			public object reasoning_effort { get; set; }
			public object max_completion_tokens { get; set; }
			public object max_prompt_tokens { get; set; }
			public TruncationStrategy truncation_strategy { get; set; }
			public object incomplete_details { get; set; }
			public Usage usage { get; set; }
			public string response_format { get; set; }
			public string tool_choice { get; set; }
			public bool parallel_tool_calls { get; set; }
		}
	}

	public class AssistantEventArgs
	{
		public AssistantEventArgs(AssistantEventType type, string message)
		{
			Type = type;
			Message = message;
		}

		public AssistantEventType Type { get; private set; }

		public string Message { get; private set; }
	}

	public enum AssistantEventType
	{
		Message
	}

	public class AssistantThread
	{
		public string id { get; set; }
		public string @object { get; set; }
		public int created_at { get; set; }
		public Metadata metadata { get; set; }
		public ToolResources tool_resources { get; set; }
	}

}