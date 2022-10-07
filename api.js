import axios from "axios";

export const api = {
  config: (state, options) => {
    const { url, method, params, data} = options;

    return {
      baseURL: window.configSettings.apiRoot,
      crossDomain: true,
      url,
      method,
      params: params ? { ...params } : {},
      data: data ? data : {}
    };
  },
  call: config => {
    return axios(config).then(response => response?.data?.Result?.Data);
  },
  fetch: config => {
    return axios(config).then(response => response);
  }
};