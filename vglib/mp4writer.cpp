#include "mp4writer.h"



Mp4Writer::Mp4Writer(std::string outputFilename, float defaultFramesPerSecond = AP4_MUX_DEFAULT_VIDEO_FRAME_RATE*1000)
{

    // convert to lower case
    std::transform(outputFilename.begin(), outputFilename.end(), outputFilename.begin(), ::tolower);

    // make sure filename contains ".mp4"
    std::size_t found = outputFilename.find(".mp4");
    if (found==std::string::npos)
    {
        m_outputFilename = outputFilename + ".mp4";
    }
    else
    {
        m_outputFilename = outputFilename;
    }


    m_frameRate = (unsigned int)(1000 * defaultFramesPerSecond);

    m_errorMsg = "None";

    // create the movie object to hold the tracks
        mp_movie = new AP4_Movie();

    // setup the brands
        m_brands.Append(AP4_FILE_BRAND_ISOM);
        m_brands.Append(AP4_FILE_BRAND_MP42);

    // create a temp file to store the sample data
        mp_sample_storage = new SampleFileStorage(m_outputFilename.c_str());
        AP4_Result result = AP4_FileByteStream::Create(mp_sample_storage->m_Filename.GetChars(),
                                                       AP4_FileByteStream::STREAM_MODE_WRITE,
                                                       mp_sample_storage->m_Stream);

        if (AP4_FAILED(result)) {
            fprintf(stderr, "ERROR: failed to create temporary sample data storage (%d)\n", result);
            return;
        }


    // create a sample table
        mp_sample_table = new AP4_SyntheticSampleTable();

}

Mp4Writer::~Mp4Writer()
{
    delete mp_sample_storage;
}



int8_t Mp4Writer::AddFrame(char *frameBuffer, uint32_t numBytes)
{
    bool eos;
    AP4_Result result;
    AP4_Size bytes_consumed = 0;
    bool found_access_unit = false;
    AP4_Size bytes_in_buffer = (AP4_Size)numBytes;
    AP4_Size offset = 0;


    do {
        AP4_AvcFrameParser::AccessUnitInfo access_unit_info;

        found_access_unit = false;

        result = m_parser.Feed(&frameBuffer[offset],
                               bytes_in_buffer,
                               bytes_consumed,
                               access_unit_info,
                               eos);

        if (AP4_FAILED(result)) {
            m_errorMsg = "ERROR: Parser Feed() failed";
            return -1;
        }

        if (access_unit_info.nal_units.ItemCount()) {
            // we got one access unit
            found_access_unit = true;
            if (Options.verbose) {
                printf("H264 Access Unit, %d NAL units, decode_order=%d, display_order=%d\n",
                       access_unit_info.nal_units.ItemCount(),
                       access_unit_info.decode_order,
                       access_unit_info.display_order);
            }

            // compute the total size of the sample data
            unsigned int sample_data_size = 0;
            for (unsigned int i=0; i<access_unit_info.nal_units.ItemCount(); i++) {
                sample_data_size += 4+access_unit_info.nal_units[i]->GetDataSize();
            }

            // store the sample data
            AP4_Position position = 0;
            mp_sample_storage->GetStream()->Tell(position);
            for (unsigned int i=0; i<access_unit_info.nal_units.ItemCount(); i++) {
                mp_sample_storage->GetStream()->WriteUI32(access_unit_info.nal_units[i]->GetDataSize());
                mp_sample_storage->GetStream()->Write(access_unit_info.nal_units[i]->GetData(), access_unit_info.nal_units[i]->GetDataSize());
            }

            // add the sample to the track
            mp_sample_table->AddSample(*mp_sample_storage->GetStream(), position, sample_data_size, 1000, 0, 0, 0, access_unit_info.is_idr);

            // remember the sample order
            m_sample_orders.Append(SampleOrder(access_unit_info.decode_order, access_unit_info.display_order));

            // free the memory buffers
            access_unit_info.Reset();
        }

        offset += bytes_consumed;
        bytes_in_buffer -= bytes_consumed;

    } while (bytes_in_buffer || found_access_unit);


}

int8_t Mp4Writer::WriteFile(int frameRate)
{
    // frameRate should be in frames/second

    // adjust the sample CTS/DTS offsets based on the sample orders
        if (m_sample_orders.ItemCount() > 1) {
            unsigned int start = 0;
            for (unsigned int i=1; i<=m_sample_orders.ItemCount(); i++) {
                if (i == m_sample_orders.ItemCount() || m_sample_orders[i].m_DisplayOrder == 0) {
                    // we got to the end of the GOP, sort it by display order
                    mp_sample_storage->SortSamples(&m_sample_orders[start], i-start);
                    start = i;
                }
            }
        }
        unsigned int max_delta = 0;
        for (unsigned int i=0; i<m_sample_orders.ItemCount(); i++) {
            if (m_sample_orders[i].m_DecodeOrder > i) {
                unsigned int delta =m_sample_orders[i].m_DecodeOrder-i;
                if (delta > max_delta) {
                    max_delta = delta;
                }
            }
        }
        for (unsigned int i=0; i<m_sample_orders.ItemCount(); i++) {
            mp_sample_table->UseSample(m_sample_orders[i].m_DecodeOrder).SetCts(1000ULL*(AP4_UI64)(i+max_delta));
        }

        // check the video parameters
        AP4_AvcSequenceParameterSet* sps = NULL;
        for (unsigned int i=0; i<=AP4_AVC_SPS_MAX_ID; i++) {
            if (m_parser.GetSequenceParameterSets()[i]) {
                sps = m_parser.GetSequenceParameterSets()[i];
                break;
            }
        }
        if (sps == NULL) {
            m_errorMsg = "ERROR: no sequence parameter set found in video";
            return -1;
        }
        unsigned int video_width = 0;
        unsigned int video_height = 0;
        sps->GetInfo(video_width, video_height);


        // collect the SPS and PPS into arrays
        AP4_Array<AP4_DataBuffer> sps_array;
        for (unsigned int i=0; i<=AP4_AVC_SPS_MAX_ID; i++) {
            if (m_parser.GetSequenceParameterSets()[i]) {
                sps_array.Append(m_parser.GetSequenceParameterSets()[i]->raw_bytes);
            }
        }
        AP4_Array<AP4_DataBuffer> pps_array;
        for (unsigned int i=0; i<=AP4_AVC_PPS_MAX_ID; i++) {
            if (m_parser.GetPictureParameterSets()[i]) {
                pps_array.Append(m_parser.GetPictureParameterSets()[i]->raw_bytes);
            }
        }

        // setup the video the sample descripton
        AP4_AvcSampleDescription* sample_description =
            new AP4_AvcSampleDescription(AP4_SAMPLE_FORMAT_AVC1,
                                         video_width,
                                         video_height,
                                         24,
                                         "h264",
                                         sps->profile_idc,
                                         sps->level_idc,
                                         sps->constraint_set0_flag<<7 |
                                         sps->constraint_set1_flag<<6 |
                                         sps->constraint_set2_flag<<5 |
                                         sps->constraint_set3_flag<<4,
                                         4,
                                         sps_array,
                                         pps_array);
        mp_sample_table->AddSampleDescription(sample_description);

        // TODO: set frame rate here
        m_frameRate = frameRate * 1000;

        AP4_UI32 movie_timescale      = 1000;
        AP4_UI32 media_timescale      = m_frameRate;
        AP4_UI64 video_track_duration = AP4_ConvertTime(1000*mp_sample_table->GetSampleCount(), media_timescale, movie_timescale);
        AP4_UI64 video_media_duration = 1000*mp_sample_table->GetSampleCount();

        // create a video track
        AP4_Track* track = new AP4_Track(AP4_Track::TYPE_VIDEO,
                                         mp_sample_table,
                                         0,                    // auto-select track id
                                         movie_timescale,      // movie time scale
                                         video_track_duration, // track duration
                                         m_frameRate,          // media time scale
                                         video_media_duration, // media duration
                                         "und",                // language
                                         video_width<<16,      // width
                                         video_height<<16      // height
                                         );

        // update the brands list
        m_brands.Append(AP4_FILE_BRAND_AVC1);


        mp_movie->AddTrack(track);




        // open the output
            AP4_Result result;
            AP4_ByteStream* output = NULL;
            result = AP4_FileByteStream::Create(m_outputFilename.c_str(),
                                                AP4_FileByteStream::STREAM_MODE_WRITE, output);
            if (AP4_FAILED(result)) {
                m_errorMsg = "ERROR: cannot open output file";
                return -1;
            }

            // create a multimedia file
            AP4_File file(mp_movie);

            // set the file type
            file.SetFileType(AP4_FILE_BRAND_MP42, 1, &m_brands[0], m_brands.ItemCount());

            // write the file to the output
            AP4_FileWriter::Write(file, *output);

            // cleanup
            output->Release();


}

AP4_String Mp4Writer::GetLastErrorMessage()
{
    return m_errorMsg;
}



