using System;

[Serializable]
public class LguHubSessionDto
{
    public bool ok;
    public string username;
    public string lguhub_base_url;
    public string unity_token;
    public int expires_in;
}

[Serializable]
public class LguHubMeDto
{
    public bool success;
    public string username;
    public string nickname;
    public string bio;
    public int points;
    public bool checked_in;
    public string error;
}

[Serializable]
public class LguHubPointsDto
{
    public bool success;
    public int points;
    public bool checked_in;
    public string error;
}

[Serializable]
public class LguHubCheckinDto
{
    public bool success;
    public int balance;
    public string error;
}

[Serializable]
public class LguHubCommunityFileDto
{
    public string filename;
    public string filepath;
    public long size;
    public string download_url;
}

[Serializable]
public class LguHubCommunityCommentDto
{
    public int id;
    public string username;
    public string content;
    public string created_at;
}

[Serializable]
public class LguHubCommunityBatchDto
{
    public int id;
    public string username;
    public string description;
    public string course_code;
    public string material_type;
    public string upload_time;
    public float avg_rating;
    public int rating_count;
    public float user_rating;
    public int total_downloads;
    public bool is_favorited;
    public int favorite_count;
    public string[] tags;
    public LguHubCommunityFileDto[] files;
    public LguHubCommunityCommentDto[] comments;
    public LguHubDownloadUrlsDto download_urls;
}

[Serializable]
public class LguHubDownloadUrlsDto
{
    public string batch_zip;
    public string batch_one_click;
}

[Serializable]
public class LguHubCommunityListDto
{
    public bool success;
    public LguHubCommunityBatchDto[] items;
    public int total;
    public int next_offset;
    public bool has_more;
    public string error;
}

[Serializable]
public class LguHubCourseDto
{
    public string course_code;
    public string school;
    public string program;
    public string semester;
    public int batch_count;
    public int question_count;
}

[Serializable]
public class LguHubCoursesDto
{
    public bool success;
    public LguHubCourseDto[] courses;
    public string error;
}

[Serializable]
public class LguHubQuestionDto
{
    public int id;
    public string username;
    public string title;
    public string content;
    public bool has_bounty;
    public string bounty_text;
    public bool is_resolved;
    public string course_tag;
    public int answer_count;
    public string created_at;
}

[Serializable]
public class LguHubQuestionsDto
{
    public bool success;
    public LguHubQuestionDto[] questions;
    public string error;
}

[Serializable]
public class LguHubAnswerDto
{
    public int id;
    public string username;
    public string content;
    public bool is_accepted;
    public int vote_count;
    public bool user_voted;
    public string created_at;
}

[Serializable]
public class LguHubQuestionDetailDto
{
    public bool success;
    public LguHubQuestionDto question;
    public LguHubAnswerDto[] answers;
    public string error;
}

[Serializable]
public class LguHubSimpleOkDto
{
    public bool success;
    public string error;
    public int question_id;
    public int answer_id;
}

[Serializable]
public class LguHubRateDto
{
    public bool success;
    public float avg_rating;
    public int rating_count;
    public string error;
}

[Serializable]
public class LguHubFavoriteDto
{
    public bool success;
    public bool is_favorited;
    public int favorite_count;
    public string error;
}

[Serializable]
public class LguHubErrorWrapper
{
    public string error;
}
