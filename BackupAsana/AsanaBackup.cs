
using BackupAsana.DTOs;
using BackupAsana.ExportDTOs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimplHelpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BackupAsana
{
    public class AsanaBackup
    {
        string userToken;
        string baseDirectory;
        bool overrideExistingFiles;

        public AsanaBackup(string userAuthToken, string _baseDirectory, bool _overrideExistingFiles)
        {
            this.userToken = "Bearer " +  userAuthToken;
            baseDirectory = _baseDirectory;
            this.overrideExistingFiles = _overrideExistingFiles;
            if (!Directory.Exists(baseDirectory))
                Directory.CreateDirectory(baseDirectory);
        }

        class MakeWebRequestResDTO
        {
            public int StatusCode { get; set; }
            public string Content { get; set; }
        }

        async Task<MakeWebRequestResDTO> MakeWebRequestAsync(string url)
        {
            try
            {
                //TODO: send token from the currentAPI user
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Headers.Add("Authorization", userToken);
                request.Method = "GET";
                request.ContentType = "application/x-www-form-urlencoded";
                
                using (var response = (HttpWebResponse)(await request.GetResponseAsync()))
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                        {
                            return new MakeWebRequestResDTO
                            {
                                StatusCode = 200,
                                Content = reader.ReadToEnd()
                            };
                        }
                    }
                    Console.WriteLine("Status code {0} on URL: {1}", response.StatusCode, url);

                    return new MakeWebRequestResDTO
                    {
                        StatusCode = (int)response.StatusCode
                        
                    };
                }
            }
            catch (Exception e)
            {

            }

            return new MakeWebRequestResDTO
            {
                StatusCode = 500
            };
            
        }


        public async Task BackupProjects(long? workspaceID)
        {
            var projectsUrl = "https://app.asana.com/api/1.0/projects/";
            var usersUrl = "https://app.asana.com/api/1.0/users";
            if (workspaceID.HasValue)
            {
                projectsUrl = projectsUrl.AddQueryParam("workspace", workspaceID.ToString());
                usersUrl = SimplHelpers.UrlHelper.Combine("https://app.asana.com/api/1.0/workspaces/", workspaceID.Value.ToString(), "users");
            }
            var projectsResponse = await MakeWebRequestAsync(projectsUrl);

            if (projectsResponse.StatusCode != 200)
                return;

            var projectsFilePath = Path.Combine(baseDirectory, "projects.json");

            if (File.Exists(projectsFilePath))
                File.Delete(projectsFilePath);

            File.WriteAllText(projectsFilePath, projectsResponse.Content);

            var usersResponse = await MakeWebRequestAsync(usersUrl);
            if (usersResponse.StatusCode != 200)
                return;

            //Backup users
            var usersFilePath = Path.Combine(baseDirectory, "users.json");
            if (File.Exists(usersFilePath))
                File.Delete(usersFilePath);

            File.WriteAllText(usersFilePath, usersResponse.Content);

            var projects = JsonConvert.DeserializeObject<IdNameValuesHolder>(projectsResponse.Content);

            foreach (var project in projects.Data)
            {
                await BackupProjectAsync(project.ID, project.Name);
            }
        }

        async Task BackupProjectAsync(long projectID, string name)
        {
            var projectBackupPath = Path.Combine(baseDirectory, projectID.ToString() + ".json");
            if (File.Exists(projectBackupPath))
                if (overrideExistingFiles)
                    File.Delete(projectBackupPath);
                else
                    return;

            var timer = Stopwatch.StartNew();
            Console.WriteLine("Backing up projct: {0}", name);
            var project = new ProjectDTO
            {
                ProjectID = projectID,
                Name = name
            };

            project.Tasks = await GetTasksForProjectAsync(projectID);
            
            var backupDataSerialized = JsonConvert.SerializeObject(project);
            
            File.WriteAllText(projectBackupPath, backupDataSerialized);

            Console.WriteLine("Project backuped in: {0}", timer.Elapsed);
        }


        async Task<List<TaskDTO>> GetTasksForProjectAsync(long projectID)
        {
            var url = SimplHelpers.UrlHelper.Combine("https://app.asana.com/api/1.0/projects/", projectID.ToString(), "tasks");

            var returnModel = new List<TaskDTO>();
            var serviceResponse = await MakeWebRequestAsync(url);
            if (serviceResponse.StatusCode != 200)
                return returnModel;

            var serverTasks = JsonConvert.DeserializeObject<IdNameValuesHolder>(serviceResponse.Content);

            var serverTasksIDs = serverTasks.Data.Select(x => x.ID).ToList();
            foreach (var serverTask in serverTasks.Data)
            {
                serverTasksIDs.AddRange(await GetSubtasks(serverTask.ID));
            }
            
            
            Task<TaskDTO>[] tasks = new Task<TaskDTO>[serverTasksIDs.Count];
            int taskIndex = 0;


            foreach (var serverTaskID in serverTasksIDs.ToHashSet())
            {
                tasks[taskIndex] = GetTask(serverTaskID);
                ++taskIndex;
            }
            Task.WaitAll(tasks);

            foreach (var task in tasks)
            {
                returnModel.Add(task.Result);
            }

            returnModel = returnModel.Where(x => !String.IsNullOrWhiteSpace(x.Name)).ToList();
            return returnModel;
        }

        async Task<List<long>> GetSubtasks(long taskID)
        {
            var returnModel = new List<long>();
            var subtasksUrl = SimplHelpers.UrlHelper.Combine("https://app.asana.com/api/1.0/tasks/", taskID.ToString(), "subtasks");
            var serverResponse = await MakeWebRequestAsync(subtasksUrl);

            if (serverResponse.StatusCode != 200)
                return returnModel;

            var subtasks = JsonConvert.DeserializeObject<IdNameValuesHolder>(serverResponse.Content);

            foreach (var subtask in subtasks.Data)
            {
                returnModel.Add(subtask.ID);
                returnModel.AddRange(await GetSubtasks(subtask.ID));
            }
            return returnModel;
        }


        async Task<TaskDTO> GetTask(long taskID)
        {
            var taskDetailsUrl = SimplHelpers.UrlHelper.Combine("https://app.asana.com/api/1.0/tasks/", taskID.ToString());

            var returnModel = new TaskDTO();
            var taskDetailsResponse = await MakeWebRequestAsync(taskDetailsUrl);
            if (taskDetailsResponse.StatusCode != 200)
                return returnModel;

            var d = JsonConvert.DeserializeObject<dynamic>(taskDetailsResponse.Content);

            try
            {
                var data = d["data"];
                returnModel.TaskID = data["id"];
                returnModel.Name = data["name"];
                returnModel.DateCreated = data["created_at"].Value;
                returnModel.IsCompleted = data["completed"].Value;
                if (data["assignee"] != null)
                    returnModel.Assignee = data["assignee"]["id"];

                if (data["parent"] != null)
                    returnModel.ParentTaskID = data["parent"]["id"];
                returnModel.Description = data["notes"];

                if (data["followers"] != null)
                    foreach (var follower in data["followers"])
                    {
                        long followerID;
                        if (long.TryParse(follower["id"].Value.ToString(), out followerID))
                            returnModel.Followers.Add(followerID);
                        else
                            Console.WriteLine("Can't parse follower {0}", follower["id"]);
                    }
                
            }
            catch (Exception e)
            {
                Console.WriteLine("Couldn't deserialize task: {0}", taskID);
            }

            var taskActivityUrl = SimplHelpers.UrlHelper.Combine("https://app.asana.com/api/1.0/tasks/", taskID.ToString(), "stories");
            var taskActivityResponse = await MakeWebRequestAsync(taskActivityUrl);
            if (taskActivityResponse.StatusCode == 200)
            {
                var taskActivities = new List<TaskActivityDTO>();
                var taskActivityServer = JsonConvert.DeserializeObject<dynamic>(taskActivityResponse.Content);
                try
                {
                    if (taskActivityServer["data"] != null)
                        foreach (var activity in taskActivityServer["data"])
                        {
                            if (activity["type"] == "comment")
                            {
                                var taksActivity = new TaskActivityDTO
                                {
                                    ID = activity["id"],
                                    CreatedBy = activity["created_by"]["id"].Value,
                                    DateActivity = activity["created_at"].Value,
                                    Text = activity["text"]
                                };

                                taskActivities.Add(taksActivity);
                            }
                        }

                    returnModel.Activities = taskActivities;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Problems with deserializing activities on task {0}", taskID);
                }
            }

            return returnModel;
        }

    }
}
